using Sandbox.ModAPI.Ingame;

namespace IngameScript
{
    public sealed class Program : MyGridProgram
    {
        #region mdk preserve
        /*
         PLAYER SETUP - LCD Sprite Drawer 2.0.1 (typed drawing, Motion and Binding)
         PB surface 0 shows status with Grid unless selected for normal drawing.
         Status colors are set once; later terminal color changes are preserved.
         Add LCDSpriteDrawer on its own CustomData line, or use
         [Tags] with LCDSpriteDrawer=true on a same-construct display block.
         After setup, copy the display block's entire CustomData into the editor.
         Paste the editor's full preserved document back into the same block.
         Back up CustomData, run setup, edit Layout, then reload to draw.
         Legacy PB Runtime limits are preserved but ignored.
         Drawing yields between surfaces and resumes on the next tick.
         setup/reload continue one block per tick. stop cancels pending work.
         setup saves sequentially and stops at the first error; prior saves remain.
         reload validates settings and starts Update10. Motion uses elapsed seconds.
         Reload briefly selects Advertisements once before drawing each surface.
         One drawing pass shares one time sample, including deferred surfaces.
         Binding reads named block values once per drawing pass. No inventories.
         Motion restarts from time zero after reload or load.
         Lost surfaces are removed; use reload to discover replacements.
         Lost Binding sources remove only dependent surfaces, keeping their last frame.
         stop leaves the last frame and display settings. Reset them manually
         in the display terminal to use another script or text display.
         Load/recompile revalidates saved Running and restarts from phase zero.
         setup, reload preparation and faults clear automatic restart eligibility.
         stop also resets corrupt private Storage. No display state is saved.

         MIT License
         Copyright (c) 2026 SunokoHub

         Permission is hereby granted, free of charge, to any person obtaining a copy
         of this software and associated documentation files (the "Software"), to deal
         in the Software without restriction, including without limitation the rights
         to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
         copies of the Software, and to permit persons to whom the Software is
         furnished to do so, subject to the following conditions:

         The above copyright notice and this permission notice shall be included in all
         copies or substantial portions of the Software.

         THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
         IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
         FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
         AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
         LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
         OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
         SOFTWARE.
        */
        #endregion
        const string ScriptVersion = "2.0.1";
        const string ReloadRefreshApp = "TSS_FactionStationAdvert";
        string state = "Stopped", fault = "", warning = "";
        System.Collections.Generic.List<DrawTarget> targets = new System.Collections.Generic.List<DrawTarget>();

        readonly SurfacePass pass = new SurfacePass();
        readonly RunStateStore runState = new RunStateStore();
        System.Collections.Generic.List<IMyTerminalBlock> pendingBlocks;
        System.Collections.Generic.List<DrawTarget> loadingTargets;
        int blockIndex, selected, saved;
        string operation = "";
        double elapsed;
        readonly System.Collections.Generic.Dictionary<string, long> names =
            new System.Collections.Generic.Dictionary<string, long>(System.StringComparer.Ordinal);
        readonly System.Collections.Generic.Dictionary<string, BindingInput> inputNames =
            new System.Collections.Generic.Dictionary<string, BindingInput>(System.StringComparer.Ordinal);
        readonly System.Collections.Generic.List<BindingInput> inputs = new System.Collections.Generic.List<BindingInput>();
        readonly System.Collections.Generic.List<BindingTrack> resolving = new System.Collections.Generic.List<BindingTrack>();
        bool indexing;
        int resolveIndex, sampleIndex;
        SetupPlan stagedPlan;
        IMyTerminalBlock stagedBlock;
        string statusText;
        int heartbeat;
        bool statusFailed;
        bool statusManaged = true;

        public Program()
        {
            try
            {
                statusManaged = SetupConfig.PreservePbDisplay(Me.CustomData);
                string reason;
                if (!runState.Load(Storage, out reason)) { Halt(reason); return; }
                if (runState.Running) Reload();
            }
            catch (System.Exception)
            { Fail("Saved run state initialization failed. Use stop before retrying."); }
            finally { UpdateStatus(false); }
        }

        public void Save()
        {
            if (runState.Usable) StoreRunState(runState.Running);
        }

        public void Main(string argument, UpdateType updateSource)
        {
            try { RunCommand(argument, updateSource); }
            finally { UpdateStatus((updateSource & UpdateType.Update10) != 0); }
        }

        void RunCommand(string argument, UpdateType updateSource)
        {
            if (state == "Running")
            {
                double next;
                if (!Motion.TryAdvance(elapsed, Runtime.TimeSinceLastRun.TotalSeconds, out next))
                { Fail("Invalid elapsed time."); if (argument != "stop") return; }
                else elapsed = next;
            }
            if ((updateSource & (UpdateType.Terminal | UpdateType.Trigger | UpdateType.Script)) == 0)
            {
                if (operation.Length != 0 && (updateSource & UpdateType.Once) != 0)
                { ContinueConfiguration(); return; }
                if (state == "Running" && (updateSource & (UpdateType.Update10 | UpdateType.Once)) != 0)
                {
                    if (pass.TryContinue((updateSource & UpdateType.Update10) != 0)) Draw();
                }
                return;
            }
            // A read-only command can coincide with and consume the scheduled Once.
            // Preserve pending work; stop, reload and faults below replace this request.
            if (operation.Length != 0 || (state == "Running" && pass.Pending))
                Runtime.UpdateFrequency |= UpdateFrequency.Once;
            if (string.IsNullOrEmpty(argument)) { ShowStatus(); return; }
            if (argument == "stop") { Stop(); ShowStatus(); return; }
            if (argument == "setup") { Setup(); return; }
            if (argument == "reload") { Reload(); return; }
            long blockId;
            if (!SurfaceInfo.TryParseInfoCommand(argument, out blockId))
            { Fail("Expected setup, reload, info <BlockId> or stop."); return; }
            try
            {
                string output, reason;
                var block = GridTerminalSystem.GetBlockWithId(blockId);
                if (!SurfaceInfo.TryReadBlock(block, Me, out output, out reason)) { Fail(reason); return; }
                Echo(output);
            }
            catch (System.Exception) { Fail("Block API or output failed. Use stop before retrying."); }
        }

        bool ValidatePbDocument()
        {
            string reason;
            if (!SetupConfig.ValidatePbDocument(Me.CustomData, out reason))
            { Fail(reason); return false; }
            return true;
        }

        void Setup() => BeginConfiguration(true);
        void Reload() => BeginConfiguration(false);

        void BeginConfiguration(bool setup)
        {
            statusFailed = false; statusText = null;
            StopControl();
            if (!StoreRunState(false)) return;
            try
            {
                statusManaged = SetupConfig.PreservePbDisplay(Me.CustomData);
                if (!ValidatePbDocument()) return;
                pendingBlocks = new System.Collections.Generic.List<IMyTerminalBlock>();
                GridTerminalSystem.GetBlocks(pendingBlocks);
                indexing = true;
                loadingTargets = new System.Collections.Generic.List<DrawTarget>();
                operation = setup ? "Setup" : "Reload";
                Runtime.UpdateFrequency = UpdateFrequency.Once;
                ShowStatus();
            }
            catch (System.Exception) { Fail("Configuration discovery failed. Use stop or reload to recover."); }
        }

        void ContinueConfiguration()
        {
            try
            {
                if (indexing)
                {
                    if (blockIndex < pendingBlocks.Count)
                    {
                        var item = pendingBlocks[blockIndex++];
                        if (item.IsSameConstructAs(Me))
                        {
                            Binding.IndexName(names, item.CustomName, item.EntityId);
                        }
                    }
                    if (blockIndex == pendingBlocks.Count) { indexing = false; blockIndex = 0; }
                    Runtime.UpdateFrequency = UpdateFrequency.Once; return;
                }
                if (stagedPlan != null)
                {
                    if (!stagedPlan.Complete)
                    {
                        string parseReason;
                        if (!SetupConfig.TryContinuePlan(stagedPlan, out parseReason))
                        { Fail("Block " + stagedBlock.EntityId + ": " + parseReason); return; }
                        if (stagedPlan.Complete)
                            foreach (var binding in stagedPlan.Bindings.Values) resolving.AddRange(binding.Tracks);
                        Runtime.UpdateFrequency = UpdateFrequency.Once; return;
                    }
                    if (resolveIndex < resolving.Count)
                    {
                        var track = resolving[resolveIndex++];
                        string key = BlockValues.Key(track.Source, track.BlockName);
                        BindingInput input;
                        if (!inputNames.TryGetValue(key, out input))
                        {
                            long referenceId = Binding.FindName(names, track.BlockName);
                            var reference = referenceId == 0 ? null : GridTerminalSystem.GetBlockWithId(referenceId);
                            if (!Binding.Matches(reference, Me, track.Source))
                            { Fail("Binding name is missing, ambiguous or has the wrong type: " + track.BlockName); return; }
                            input = new BindingInput { BlockId = reference.EntityId, Source = track.Source };
                            inputs.Add(input); inputNames.Add(key, input);
                        }
                        track.Input = input; input.Users++;
                        Runtime.UpdateFrequency = UpdateFrequency.Once; return;
                    }
                    if (!stagedBlock.IsSameConstructAs(Me)) { Fail("Configuration block left this construct."); return; }
                    string reason;
                    if (operation == "Setup")
                    {
                        foreach (var layout in stagedPlan.Layouts)
                            if (GetSurface(stagedBlock, layout.Key) == null)
                            { Fail("Configuration surface is unavailable."); return; }
                        var savedBlock = stagedBlock;
                        stagedPlan.Read = () => savedBlock.CustomData; stagedPlan.Write = value => savedBlock.CustomData = value;
                        int written;
                        if (!SetupConfig.TrySaveAll(new System.Collections.Generic.List<SetupPlan> { stagedPlan }, out written, out reason))
                        { Fail("Block " + stagedBlock.EntityId + ": " + reason); return; }
                        selected++; saved += written;
                    }
                    else foreach (var layout in stagedPlan.Layouts)
                    {
                        foreach (var track in stagedPlan.Bindings[layout.Key].Tracks)
                            stagedPlan.Bindings[layout.Key].Inputs.Add(track.Input);
                        loadingTargets.Add(new DrawTarget { BlockId = stagedBlock.EntityId, Index = layout.Key,
                            Elements = DrawingData.Build(layout.Value), Layout = layout.Value,
                            Binding = stagedPlan.Bindings[layout.Key] });
                    }
                    stagedPlan = null; stagedBlock = null; resolving.Clear(); resolveIndex = 0;
                    Runtime.UpdateFrequency = UpdateFrequency.Once; return;
                }
                if (blockIndex < pendingBlocks.Count)
                {
                    var block = pendingBlocks[blockIndex++];
                    if (block.IsSameConstructAs(Me))
                    {
                        string source = block.CustomData;
                        if (SetupConfig.HasSelectionIntent(source))
                        {
                            bool setup = operation == "Setup";
                            string reason;
                            SetupPlan plan;
                            var provider = block as IMyTextSurfaceProvider;
                            int count = provider != null ? provider.SurfaceCount : block is IMyTextSurface ? 1 : 0;
                            string metadata = null;
                            if (setup && SurfaceInfo.Evaluate(source, out reason) == BlockSelectionStatus.Selected &&
                                !SurfaceInfo.TryReadBlock(block, Me, out metadata, out reason))
                            { Fail("Block " + block.EntityId + ": " + reason); return; }
                            if (!SetupConfig.TryBeginPlan(source, Me.EntityId, count, out plan, out reason, setup, metadata))
                            { Fail("Block " + block.EntityId + ": " + reason); return; }
                            if (plan != null)
                            {
                                stagedPlan = plan; stagedBlock = block;
                                Runtime.UpdateFrequency = UpdateFrequency.Once; return;
                            }
                        }
                    }
                }
                if (blockIndex < pendingBlocks.Count)
                { Runtime.UpdateFrequency = UpdateFrequency.Once; return; }
                if (operation == "Setup")
                {
                    string result = "Setup complete. Selected=" + selected + " Saved=" + saved + " State=Stopped";
                    CancelConfiguration();
                    ClearInputs();
                    Runtime.UpdateFrequency = UpdateFrequency.None;
                    Echo(result);
                }
                else
                {
                    if (!StoreRunState(true)) return;
                    targets = loadingTargets;
                    CancelConfiguration();
                    state = "Running";
                    pass.Begin();
                    // Do not draw after discovery, parsing or saving in this invocation.
                    Runtime.UpdateFrequency = UpdateFrequency.Update10 | UpdateFrequency.Once;
                    ShowStatus();
                }
            }
            catch (System.Exception)
            { Fail("Configuration API failed. Earlier saves remain; later saves stopped."); }
        }

        void CancelConfiguration()
        {
            operation = ""; pendingBlocks = null; loadingTargets = null;
            blockIndex = 0; selected = 0; saved = 0;
            indexing = false; names.Clear(); stagedPlan = null; stagedBlock = null;
            resolving.Clear(); resolveIndex = 0;
        }

        bool Draw()
        {
            DrawTarget current = null;
            double time = pass.Sample(elapsed);
            try
            {
                while (sampleIndex < inputs.Count)
                {
                    if (!SurfacePass.CanStart(Runtime.CurrentInstructionCount, Runtime.MaxInstructionCount, 0))
                    { Runtime.UpdateFrequency = UpdateFrequency.Update10 | UpdateFrequency.Once; return true; }
                    var input = inputs[sampleIndex];
                    if (input.Users == 0 || input.Lost) { sampleIndex++; continue; }
                    var reference = GridTerminalSystem.GetBlockWithId(input.BlockId);
                    if (!Binding.Matches(reference, Me, input.Source))
                    { input.Lost = true; sampleIndex++; continue; }
                    string sampleReason;
                    if (!BlockValues.TryRead(reference, input.Source, out input.Value, out input.Enabled, out sampleReason))
                    { Fail("Binding " + input.Source + " Block " + input.BlockId + ": " + sampleReason); return false; }
                    sampleIndex++;
                }
                while (pass.Pending && pass.Index < targets.Count)
                {
                    current = targets[pass.Index];
                    if (SurfacePass.RequiredInstructions(current.Elements.Count) >= Runtime.MaxInstructionCount)
                    { Fail(TargetReason(current, "Surface exceeds the available instruction budget.")); return false; }
                    if (!SurfacePass.CanStart(Runtime.CurrentInstructionCount,
                        Runtime.MaxInstructionCount, current.Elements.Count))
                    {
                        Runtime.UpdateFrequency = UpdateFrequency.Update10 | UpdateFrequency.Once;
                        return true;
                    }
                    // No yield from this point until the entire surface is submitted.
                    var block = GridTerminalSystem.GetBlockWithId(current.BlockId);
                    var surface = block == null ? null : GetSurface(block, current.Index);
                    if (surface == null)
                    { foreach (var track in current.Binding.Tracks) track.Input.Users--; targets.RemoveAt(pass.Index); ReportLost(current); continue; }
                    if (!block.IsSameConstructAs(Me))
                    { Fail(TargetReason(current, "Drawing surface left this construct.")); return false; }
                    string reason;
                    var elements = current.Elements;
                    foreach (var input in current.Binding.Inputs)
                        if (!input.Lost && !Binding.Matches(GridTerminalSystem.GetBlockWithId(input.BlockId), Me, input.Source))
                            input.Lost = true;
                    if (current.Binding.SourceLost)
                    {
                        foreach (var track in current.Binding.Tracks) track.Input.Users--;
                        targets.RemoveAt(pass.Index);
                        warning = TargetReason(current, "Binding source lost or left this construct. Surface removed; remaining surfaces keep updating. Use reload for recovery.");
                        ShowStatus(); continue;
                    }
                    if (current.Binding.Tracks.Count > 0 || current.Binding.Combined.Count > 0)
                    {
                        if (!Binding.TryBuild(current.Layout, current.Binding, time, out elements, out reason))
                        { Fail(TargetReason(current, reason)); return false; }
                    }
                    // Leave the built-in app active until a later drawing pass.
                    if (current.RefreshPending)
                    {
                        surface.ContentType = VRage.Game.GUI.TextPanel.ContentType.SCRIPT;
                        surface.Script = ReloadRefreshApp;
                        current.RefreshPending = false;
                        pass.Advance(); continue;
                    }
                    if (!SpriteDrawing.TryDrawElements(block, current.Index, elements, out reason))
                    { Fail(TargetReason(current, reason)); return false; }
                    pass.Advance();
                }
                pass.Cancel();
                sampleIndex = 0;
                Runtime.UpdateFrequency = UpdateFrequency.Update10;
                return true;
            }
            catch (System.Exception)
            { Fail(current == null ? "Drawing API failed." : TargetReason(current, "Drawing API failed. Earlier frames remain.")); return false; }
        }

        void ClearInputs()
        { inputs.Clear(); inputNames.Clear(); sampleIndex = 0; }

        void Stop()
        {
            StopControl();
            StoreRunState(false, true);
        }

        void StopControl()
        {
            Runtime.UpdateFrequency = UpdateFrequency.None;
            state = "Stopped"; fault = ""; warning = ""; pass.Cancel(); CancelConfiguration(); targets.Clear();
            elapsed = 0;
            ClearInputs();
        }

        static IMyTextSurface GetSurface(IMyTerminalBlock block, int index)
        {
            var provider = block as IMyTextSurfaceProvider;
            if (provider != null) return index >= 0 && index < provider.SurfaceCount ? provider.GetSurface(index) : null;
            return index == 0 ? block as IMyTextSurface : null;
        }

        static string TargetReason(DrawTarget target, string reason) => "Block " + target.BlockId + " Surface " + target.Index + ": " + reason;

        void Fail(string reason)
        {
            Halt(reason);
            if (runState.Usable) StoreRunState(false);
        }

        bool StoreRunState(bool running, bool reset = false)
        {
            string reason;
            if (runState.Commit(running, () => Storage, value => Storage = value, out reason, reset)) return true;
            Halt(reason);
            return false;
        }

        void Halt(string reason)
        { Runtime.UpdateFrequency = UpdateFrequency.None; pass.Cancel(); CancelConfiguration(); ClearInputs(); state = "Faulted"; fault = reason; ShowStatus(); }

        void ReportLost(DrawTarget target)
        {
            warning = TargetReason(target, "Lost surface removed. Remaining surfaces keep updating; use reload for replacements.");
            ShowStatus();
        }

        void UpdateStatus(bool tick)
        {
            if (statusFailed || statusManaged) return;
            if (state == "Running" && tick) heartbeat = (heartbeat + 1) % 24;
            else if (state != "Running") heartbeat = 0;
            string text = "LCD Sprite Drawer\n" + ScriptVersion + "\n" +
                (operation.Length == 0 ? state.ToUpperInvariant() : operation.ToUpperInvariant()) +
                " " + (state == "Running" ? "|/-\\"[heartbeat / 6].ToString() : "-");
            if (text == statusText) return;
            try
            {
                var surface = Me.GetSurface(0);
                if (runState.Usable && !runState.StatusColorsInitialized)
                {
                    surface.FontColor = new VRageMath.Color(179, 237, 255);
                    surface.BackgroundColor = new VRageMath.Color(0, 88, 151);
                    string reason;
                    if (!runState.InitializeStatusColors(() => Storage, value => Storage = value, out reason))
                    { statusFailed = true; Halt(reason); return; }
                }
                if (statusText == null) { surface.ClearImagesFromSelection(); surface.AddImageToSelection("Grid"); }
                surface.ContentType = VRage.Game.GUI.TextPanel.ContentType.TEXT_AND_IMAGE;
                surface.Script = ""; surface.Font = "Monospace"; surface.FontSize = 1.0f;
                surface.Alignment = VRage.Game.GUI.TextPanel.TextAlignment.CENTER; surface.TextPadding = 0;
                float line = surface.MeasureStringInPixels(new System.Text.StringBuilder("X"), surface.Font, surface.FontSize).Y;
                float height = surface.MeasureStringInPixels(new System.Text.StringBuilder(text), surface.Font, surface.FontSize).Y;
                int rows = line > 0 ? (int)System.Math.Max(0, (surface.SurfaceSize.Y - height) / (2 * line)) : 0;
                if (!surface.WriteText(new string('\n', rows) + text, false)) throw new System.InvalidOperationException();
                statusText = text;
            }
            catch (System.Exception)
            { statusFailed = true; Echo("PB status display failed. Use reload to retry."); }
        }

        void ShowStatus() => Echo("LCD Sprite Drawer " + ScriptVersion + " typed-binding\nState=" + state + "\nOperation=" + operation + "\nSurfaces=" + targets.Count +
            "\nTime=" + elapsed.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture) + "\n" + fault + "\n" + warning);
    }
}
