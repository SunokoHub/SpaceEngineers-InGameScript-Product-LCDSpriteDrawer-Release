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
    const string ScriptVersion="2.0.1",ReloadRefreshApp="TSS_FactionStationAdvert";string state="Stopped",fault="",
warning="",operation="",statusText;System.Collections.Generic.List<DrawTarget>targets=new System.Collections.Generic.List<
DrawTarget>(),loadingTargets;SurfacePass pass=new SurfacePass();RunStateStore runState=new RunStateStore();System.Collections.
Generic.List<IMyTerminalBlock>pendingBlocks;int blockIndex,selected,saved,resolveIndex,sampleIndex,heartbeat;double elapsed;
System.Collections.Generic.Dictionary<string,long>names=new System.Collections.Generic.Dictionary<string,long>(System.
StringComparer.Ordinal);System.Collections.Generic.Dictionary<string,BindingInput>inputNames=new System.Collections.Generic.Dictionary
<string,BindingInput>(System.StringComparer.Ordinal);System.Collections.Generic.List<BindingInput>inputs=new System.
Collections.Generic.List<BindingInput>();System.Collections.Generic.List<BindingTrack>resolving=new System.Collections.Generic.List
<BindingTrack>();bool indexing,statusFailed,statusManaged=true;SetupPlan stagedPlan;IMyTerminalBlock stagedBlock;public
 Program
(){try{statusManaged=SetupConfig.PreservePbDisplay(Me.CustomData);string reason;if(!runState.Load(Storage,out reason)){
Halt(reason);return;}if(runState.Running)Reload();}catch(System.Exception){Fail(
"Saved run state initialization failed. Use stop before retrying.");}finally{UpdateStatus(false);}}public void
 Save
(){if(runState.Usable)StoreRunState(runState.Running);}public void
 Main
(string argument,UpdateType updateSource){try{RunCommand(argument,updateSource);}finally{UpdateStatus((updateSource&
UpdateType.Update10)!=0);}}void RunCommand(string argument,UpdateType updateSource){if(state=="Running"){double next;if(!Motion.
TryAdvance(elapsed,Runtime.TimeSinceLastRun.TotalSeconds,out next)){Fail("Invalid elapsed time.");if(argument!="stop")return;}else
elapsed=next;}if((updateSource&(UpdateType.Terminal|UpdateType.Trigger|UpdateType.Script))==0){if(operation.Length!=0&&(
updateSource&UpdateType.Once)!=0){ContinueConfiguration();return;}if(state=="Running"&&(updateSource&(UpdateType.Update10|UpdateType
.Once))!=0){if(pass.TryContinue((updateSource&UpdateType.Update10)!=0))Draw();}return;}if(operation.Length!=0||(state==
"Running"&&pass.Pending))Runtime.UpdateFrequency|=UpdateFrequency.Once;if(string.IsNullOrEmpty(argument)){ShowStatus();return;}if
(argument=="stop"){Stop();ShowStatus();return;}if(argument=="setup"){Setup();return;}if(argument=="reload"){Reload();
return;}long blockId;if(!SurfaceInfo.TryParseInfoCommand(argument,out blockId)){Fail(
"Expected setup, reload, info <BlockId> or stop.");return;}try{string output,reason;var block=GridTerminalSystem.GetBlockWithId(blockId);if(!SurfaceInfo.TryReadBlock(
block,Me,out output,out reason)){Fail(reason);return;}Echo(output);}catch(System.Exception){Fail(
"Block API or output failed. Use stop before retrying.");}}bool ValidatePbDocument(){string reason;if(!SetupConfig.ValidatePbDocument(Me.CustomData,out reason)){Fail(reason);
return false;}return true;}void Setup()=>BeginConfiguration(true);void Reload()=>BeginConfiguration(false);void
BeginConfiguration(bool setup){statusFailed=false;statusText=null;StopControl();if(!StoreRunState(false))return;try{statusManaged=
SetupConfig.PreservePbDisplay(Me.CustomData);if(!ValidatePbDocument())return;pendingBlocks=new System.Collections.Generic.List<
IMyTerminalBlock>();GridTerminalSystem.GetBlocks(pendingBlocks);indexing=true;loadingTargets=new System.Collections.Generic.List<
DrawTarget>();operation=setup?"Setup":"Reload";Runtime.UpdateFrequency=UpdateFrequency.Once;ShowStatus();}catch(System.Exception){
Fail("Configuration discovery failed. Use stop or reload to recover.");}}void ContinueConfiguration(){try{if(indexing){if(
blockIndex<pendingBlocks.Count){var item=pendingBlocks[blockIndex++];if(item.IsSameConstructAs(Me)){Binding.IndexName(names,item.
CustomName,item.EntityId);}}if(blockIndex==pendingBlocks.Count){indexing=false;blockIndex=0;}Runtime.UpdateFrequency=
UpdateFrequency.Once;return;}if(stagedPlan!=null){if(!stagedPlan.Complete){string parseReason;if(!SetupConfig.TryContinuePlan(
stagedPlan,out parseReason)){Fail("Block "+stagedBlock.EntityId+": "+parseReason);return;}if(stagedPlan.Complete)foreach(var
binding in stagedPlan.Bindings.Values)resolving.AddRange(binding.Tracks);Runtime.UpdateFrequency=UpdateFrequency.Once;return;}
if(resolveIndex<resolving.Count){var track=resolving[resolveIndex++];string key=BlockValues.Key(track.Source,track.
BlockName);BindingInput input;if(!inputNames.TryGetValue(key,out input)){long referenceId=Binding.FindName(names,track.BlockName)
;var reference=referenceId==0?null:GridTerminalSystem.GetBlockWithId(referenceId);if(!Binding.Matches(reference,Me,track.
Source)){Fail("Binding name is missing, ambiguous or has the wrong type: "+track.BlockName);return;}input=new BindingInput{
BlockId=reference.EntityId,Source=track.Source};inputs.Add(input);inputNames.Add(key,input);}track.Input=input;input.Users++;
Runtime.UpdateFrequency=UpdateFrequency.Once;return;}if(!stagedBlock.IsSameConstructAs(Me)){Fail(
"Configuration block left this construct.");return;}string reason;if(operation=="Setup"){foreach(var layout in stagedPlan.Layouts)if(GetSurface(stagedBlock,layout
.Key)==null){Fail("Configuration surface is unavailable.");return;}var savedBlock=stagedBlock;stagedPlan.Read=()=>
savedBlock.CustomData;stagedPlan.Write=value=>savedBlock.CustomData=value;int written;if(!SetupConfig.TrySaveAll(new System.
Collections.Generic.List<SetupPlan>{stagedPlan},out written,out reason)){Fail("Block "+stagedBlock.EntityId+": "+reason);return;}
selected++;saved+=written;}else foreach(var layout in stagedPlan.Layouts){foreach(var track in stagedPlan.Bindings[layout.Key].
Tracks)stagedPlan.Bindings[layout.Key].Inputs.Add(track.Input);loadingTargets.Add(new DrawTarget{BlockId=stagedBlock.EntityId,
Index=layout.Key,Elements=DrawingData.Build(layout.Value),Layout=layout.Value,Binding=stagedPlan.Bindings[layout.Key]});}
stagedPlan=null;stagedBlock=null;resolving.Clear();resolveIndex=0;Runtime.UpdateFrequency=UpdateFrequency.Once;return;}if(
blockIndex<pendingBlocks.Count){var block=pendingBlocks[blockIndex++];if(block.IsSameConstructAs(Me)){string source=block.
CustomData;if(SetupConfig.HasSelectionIntent(source)){bool setup=operation=="Setup";string reason;SetupPlan plan;var provider=
block as IMyTextSurfaceProvider;int count=provider!=null?provider.SurfaceCount:block is IMyTextSurface?1:0;string metadata=
null;if(setup&&SurfaceInfo.Evaluate(source,out reason)==BlockSelectionStatus.Selected&&!SurfaceInfo.TryReadBlock(block,Me,
out metadata,out reason)){Fail("Block "+block.EntityId+": "+reason);return;}if(!SetupConfig.TryBeginPlan(source,Me.EntityId
,count,out plan,out reason,setup,metadata)){Fail("Block "+block.EntityId+": "+reason);return;}if(plan!=null){stagedPlan=
plan;stagedBlock=block;Runtime.UpdateFrequency=UpdateFrequency.Once;return;}}}}if(blockIndex<pendingBlocks.Count){Runtime.
UpdateFrequency=UpdateFrequency.Once;return;}if(operation=="Setup"){string result="Setup complete. Selected="+selected+" Saved="+saved+
" State=Stopped";CancelConfiguration();ClearInputs();Runtime.UpdateFrequency=UpdateFrequency.None;Echo(result);}else{if(!StoreRunState(
true))return;targets=loadingTargets;CancelConfiguration();state="Running";pass.Begin();Runtime.UpdateFrequency=
UpdateFrequency.Update10|UpdateFrequency.Once;ShowStatus();}}catch(System.Exception){Fail(
"Configuration API failed. Earlier saves remain; later saves stopped.");}}void CancelConfiguration(){operation="";pendingBlocks=null;loadingTargets=null;blockIndex=0;selected=0;saved=0;
indexing=false;names.Clear();stagedPlan=null;stagedBlock=null;resolving.Clear();resolveIndex=0;}bool Draw(){DrawTarget current=
null;double time=pass.Sample(elapsed);try{while(sampleIndex<inputs.Count){if(!SurfacePass.CanStart(Runtime.
CurrentInstructionCount,Runtime.MaxInstructionCount,0)){Runtime.UpdateFrequency=UpdateFrequency.Update10|UpdateFrequency.Once;return true;}var
input=inputs[sampleIndex];if(input.Users==0||input.Lost){sampleIndex++;continue;}var reference=GridTerminalSystem.
GetBlockWithId(input.BlockId);if(!Binding.Matches(reference,Me,input.Source)){input.Lost=true;sampleIndex++;continue;}string
sampleReason;if(!BlockValues.TryRead(reference,input.Source,out input.Value,out input.Enabled,out sampleReason)){Fail("Binding "+
input.Source+" Block "+input.BlockId+": "+sampleReason);return false;}sampleIndex++;}while(pass.Pending&&pass.Index<targets.
Count){current=targets[pass.Index];if(SurfacePass.RequiredInstructions(current.Elements.Count)>=Runtime.MaxInstructionCount){
Fail(TargetReason(current,"Surface exceeds the available instruction budget."));return false;}if(!SurfacePass.CanStart(
Runtime.CurrentInstructionCount,Runtime.MaxInstructionCount,current.Elements.Count)){Runtime.UpdateFrequency=UpdateFrequency.
Update10|UpdateFrequency.Once;return true;}var block=GridTerminalSystem.GetBlockWithId(current.BlockId);var surface=block==null?
null:GetSurface(block,current.Index);if(surface==null){foreach(var track in current.Binding.Tracks)track.Input.Users--;
targets.RemoveAt(pass.Index);ReportLost(current);continue;}if(!block.IsSameConstructAs(Me)){Fail(TargetReason(current,
"Drawing surface left this construct."));return false;}string reason;var elements=current.Elements;foreach(var input in current.Binding.Inputs)if(!input.Lost
&&!Binding.Matches(GridTerminalSystem.GetBlockWithId(input.BlockId),Me,input.Source))input.Lost=true;if(current.Binding.
SourceLost){foreach(var track in current.Binding.Tracks)track.Input.Users--;targets.RemoveAt(pass.Index);warning=TargetReason(
current,"Binding source lost or left this construct. Surface removed; remaining surfaces keep updating. Use reload for recovery."
);ShowStatus();continue;}if(current.Binding.Tracks.Count>0||current.Binding.Combined.Count>0){if(!Binding.TryBuild(
current.Layout,current.Binding,time,out elements,out reason)){Fail(TargetReason(current,reason));return false;}}if(current.
RefreshPending){surface.ContentType=VRage.Game.GUI.TextPanel.ContentType.SCRIPT;surface.Script=ReloadRefreshApp;current.RefreshPending
=false;pass.Advance();continue;}if(!SpriteDrawing.TryDrawElements(block,current.Index,elements,out reason)){Fail(
TargetReason(current,reason));return false;}pass.Advance();}pass.Cancel();sampleIndex=0;Runtime.UpdateFrequency=UpdateFrequency.
Update10;return true;}catch(System.Exception){Fail(current==null?"Drawing API failed.":TargetReason(current,
"Drawing API failed. Earlier frames remain."));return false;}}void ClearInputs(){inputs.Clear();inputNames.Clear();sampleIndex=0;}void Stop(){StopControl();
StoreRunState(false,true);}void StopControl(){Runtime.UpdateFrequency=UpdateFrequency.None;state="Stopped";fault="";warning="";pass.
Cancel();CancelConfiguration();targets.Clear();elapsed=0;ClearInputs();}static IMyTextSurface GetSurface(IMyTerminalBlock
block,int index){var provider=block as IMyTextSurfaceProvider;if(provider!=null)return index>=0&&index<provider.SurfaceCount?
provider.GetSurface(index):null;return index==0?block as IMyTextSurface:null;}static string TargetReason(DrawTarget target,
string reason)=>"Block "+target.BlockId+" Surface "+target.Index+": "+reason;void Fail(string reason){Halt(reason);if(runState
.Usable)StoreRunState(false);}bool StoreRunState(bool running,bool reset=false){string reason;if(runState.Commit(running,
()=>Storage,value=>Storage=value,out reason,reset))return true;Halt(reason);return false;}void Halt(string reason){
Runtime.UpdateFrequency=UpdateFrequency.None;pass.Cancel();CancelConfiguration();ClearInputs();state="Faulted";fault=reason;
ShowStatus();}void ReportLost(DrawTarget target){warning=TargetReason(target,
"Lost surface removed. Remaining surfaces keep updating; use reload for replacements.");ShowStatus();}void UpdateStatus(bool tick){if(statusFailed||statusManaged)return;if(state=="Running"&&tick)heartbeat=(
heartbeat+1)%24;else if(state!="Running")heartbeat=0;string text="LCD Sprite Drawer\n"+ScriptVersion+"\n"+(operation.Length==0?
state.ToUpperInvariant():operation.ToUpperInvariant())+" "+(state=="Running"?"|/-\\"[heartbeat/6].ToString():"-");if(text==
statusText)return;try{var surface=Me.GetSurface(0);if(runState.Usable&&!runState.StatusColorsInitialized){surface.FontColor=new
VRageMath.Color(179,237,255);surface.BackgroundColor=new VRageMath.Color(0,88,151);string reason;if(!runState.
InitializeStatusColors(()=>Storage,value=>Storage=value,out reason)){statusFailed=true;Halt(reason);return;}}if(statusText==null){surface.
ClearImagesFromSelection();surface.AddImageToSelection("Grid");}surface.ContentType=VRage.Game.GUI.TextPanel.ContentType.TEXT_AND_IMAGE;surface.
Script="";surface.Font="Monospace";surface.FontSize=1.0f;surface.Alignment=VRage.Game.GUI.TextPanel.TextAlignment.CENTER;
surface.TextPadding=0;float line=surface.MeasureStringInPixels(new System.Text.StringBuilder("X"),surface.Font,surface.FontSize
).Y;float height=surface.MeasureStringInPixels(new System.Text.StringBuilder(text),surface.Font,surface.FontSize).Y;int
rows=line>0?(int)System.Math.Max(0,(surface.SurfaceSize.Y-height)/(2*line)):0;if(!surface.WriteText(new string('\n',rows)+
text,false))throw new System.InvalidOperationException();statusText=text;}catch(System.Exception){statusFailed=true;Echo(
"PB status display failed. Use reload to retry.");}}void ShowStatus()=>Echo("LCD Sprite Drawer "+ScriptVersion+" typed-binding\nState="+state+"\nOperation="+operation+
"\nSurfaces="+targets.Count+"\nTime="+elapsed.ToString("0.000",System.Globalization.CultureInfo.InvariantCulture)+"\n"+fault+"\n"+
warning);
}
public sealed class SurfaceInfoRecord{public string DisplayName;public VRageMath.Vector2 TextureSize,SurfaceSize;public
VRageMath.Color ForegroundColor,BackgroundColor;}public static class SurfaceInfo{public const string Tag="LCDSpriteDrawer";public
const int MaxCustomData=32000,MaxOutput=8000;public static bool TryParseInfoCommand(string argument,out long blockId){blockId
=0;if(argument==null||!argument.StartsWith("info ",StringComparison.Ordinal))return false;string number=argument.
Substring(5);if(number.Length==0||number.Length>19)return false;foreach(char c in number)if(c<'0'||c>'9')return false;return long
.TryParse(number,System.Globalization.NumberStyles.None,System.Globalization.CultureInfo.InvariantCulture,out blockId)&&
blockId>0;}public static BlockSelectionStatus Evaluate(string source,out string reason){reason="";if(source==null||source.
Length>MaxCustomData){reason="Missing or oversized CustomData.";return BlockSelectionStatus.Invalid;}CustomDataDocument
document;if(!CustomDataDocument.TryParse(source,new VRage.Game.ModAPI.Ingame.Utilities.MyIniKey[0],new[]{Tag},out document,out
reason))return BlockSelectionStatus.Invalid;return BlockSelection.Evaluate(document,new VRage.Game.ModAPI.Ingame.Utilities.
MyIniKey("Tags",Tag),Tag,out reason);}public static bool TryReadBlock(IMyTerminalBlock block,IMyTerminalBlock pb,out string
output,out string reason){output=null;reason="";if(block==null||pb==null)return Reject("Block not found.",out reason);if(!
block.IsSameConstructAs(pb))return Reject("Block is outside this construct.",out reason);if(Evaluate(block.CustomData,out
reason)!=BlockSelectionStatus.Selected){if(reason.Length==0)reason="Block is not selected.";return false;}var provider=block
as IMyTextSurfaceProvider;var direct=block as IMyTextSurface;int count=provider!=null?provider.SurfaceCount:direct!=null?1
:0;if(count<1)return Reject("No text surfaces available.",out reason);var surfaces=new List<SurfaceInfoRecord>();for(int
i=0;i<count;i++){var surface=provider!=null?provider.GetSurface(i):direct;if(surface==null)return Reject(
"Surface is unavailable.",out reason);surfaces.Add(new SurfaceInfoRecord{DisplayName=surface.DisplayName,TextureSize=surface.TextureSize,
SurfaceSize=surface.SurfaceSize,ForegroundColor=surface.ScriptForegroundColor,BackgroundColor=surface.ScriptBackgroundColor});}
return TryBuild(block.EntityId,block.CustomName,block.BlockDefinition.ToString(),surfaces,out output,out reason);}public
static bool TryBuild(long blockId,string blockName,string blockType,List<SurfaceInfoRecord>surfaces,out string output,out
string reason){output=null;reason="";if(blockId<=0||blockName==null||blockType==null||surfaces==null||surfaces.Count<1)return
Reject("Invalid block metadata or surface count.",out reason);var ini=new VRage.Game.ModAPI.Ingame.Utilities.MyIni();ini.Set(
"SurfaceInfo","SurfaceInfoFormatVersion",1);ini.Set("SurfaceInfo","BlockId",blockId);ini.Set("SurfaceInfo","BlockName",blockName);ini
.Set("SurfaceInfo","BlockType",blockType);ini.Set("SurfaceInfo","SurfaceCount",surfaces.Count);for(int i=0;i<surfaces.
Count;i++){var surface=surfaces[i];if(surface==null||surface.DisplayName==null||!Finite(surface.TextureSize)||!Finite(surface
.SurfaceSize))return Reject("Invalid surface metadata.",out reason);string section="SurfaceInfo.Surface."+i.ToString(
System.Globalization.CultureInfo.InvariantCulture);ini.Set(section,"DisplayName",surface.DisplayName);ini.Set(section,
"TextureSize",Vector(surface.TextureSize));ini.Set(section,"SurfaceSize",Vector(surface.SurfaceSize));ini.Set(section,
"ForegroundColor",Color(surface.ForegroundColor));ini.Set(section,"BackgroundColor",Color(surface.BackgroundColor));}string candidate=ini
.ToString();if(candidate.Length>MaxOutput)return Reject("Surface info exceeds 8000 characters.",out reason);output=
candidate;return true;}static bool Finite(VRageMath.Vector2 value)=>!float.IsNaN(value.X)&&!float.IsInfinity(value.X)&&!float.
IsNaN(value.Y)&&!float.IsInfinity(value.Y);static string Vector(VRageMath.Vector2 value)=>value.X.ToString("R",System.
Globalization.CultureInfo.InvariantCulture)+","+value.Y.ToString("R",System.Globalization.CultureInfo.InvariantCulture);static string
Color(VRageMath.Color value)=>value.R.ToString(System.Globalization.CultureInfo.InvariantCulture)+","+value.G.ToString(System
.Globalization.CultureInfo.InvariantCulture)+","+value.B.ToString(System.Globalization.CultureInfo.InvariantCulture)+","+
value.A.ToString(System.Globalization.CultureInfo.InvariantCulture);static bool Reject(string message,out string reason){
reason=message;return false;}}public sealed class SetupPlan{public CustomDataDocument Document;public int SurfaceCount;public
Dictionary<int,List<LayoutElement>>Layouts;public Dictionary<int,MotionSet>Motions;public Dictionary<int,BindingSet>Bindings;
public Func<string>Read;public Action<string>Write;internal List<int>Surfaces;internal List<VRage.Game.ModAPI.Ingame.Utilities
.MyIniKey>Keys;internal string Source;internal bool Setup;internal int ParseStep;public bool Complete;}public static
class SetupConfig{const string Settings="Settings.LCDSpriteDrawer";public static bool PreservePbDisplay(string source){if(!
HasSelectionIntent(source))return false;CustomDataDocument document;string reason,value;if(!CustomDataDocument.TryParse(source,new VRage.
Game.ModAPI.Ingame.Utilities.MyIniKey[0],new[]{SurfaceInfo.Tag},out document,out reason))return true;var selection=
BlockSelection.Evaluate(document,Key("Tags",SurfaceInfo.Tag),SurfaceInfo.Tag,out reason);if(selection==BlockSelectionStatus.
NotSelected)return false;if(selection==BlockSelectionStatus.Invalid||!document.TryGet(Key(Settings,"Surfaces"),out value))return
true;foreach(string item in value.Split(',')){long index;if(!NonnegativeInteger(item,out index)||index==0)return true;}
return false;}public static bool ValidatePbDocument(string source,out string reason){reason="";if(source==null||source.Length>
SurfaceInfo.MaxCustomData)return Reject("Missing or oversized PB CustomData.",out reason);CustomDataDocument document;return
CustomDataDocument.TryParse(source,new VRage.Game.ModAPI.Ingame.Utilities.MyIniKey[0],new[]{SurfaceInfo.Tag},out document,out reason);}
public static bool HasSelectionIntent(string source){if(source==null)return false;string section="";foreach(string raw in
source.Split('\n')){string line=raw.Trim();if(line=="---")break;if(line==SurfaceInfo.Tag)return true;if(line.Length==0||line[0
]==';'||line[0]=='|')continue;if(line[0]=='['&&line.EndsWith("]",StringComparison.Ordinal))section=line.Substring(1,line.
Length-2);else if(string.Equals(section,"Tags",StringComparison.OrdinalIgnoreCase)){int equals=line.IndexOf('=');if(equals>=0
&&string.Equals(line.Substring(0,equals).Trim(),SurfaceInfo.Tag,StringComparison.OrdinalIgnoreCase))return true;}}return
false;}public static bool TryPlan(string source,long owner,int availableSurfaces,out SetupPlan plan,out string reason,bool
setup=true){if(!TryBeginPlan(source,owner,availableSurfaces,out plan,out reason,setup))return false;while(plan!=null&&!plan.
Complete)if(!TryContinuePlan(plan,out reason)){plan=null;return false;}return true;}public static bool TryBeginPlan(string
source,long owner,int availableSurfaces,out SetupPlan plan,out string reason,bool setup=true,string surfaceInfo=null){plan=
null;reason="";if(source==null||source.Length>SurfaceInfo.MaxCustomData||owner<=0)return Reject(
"Missing/oversized CustomData or invalid PB ID.",out reason);var keys=new List<VRage.Game.ModAPI.Ingame.Utilities.MyIniKey>();keys.Add(Key("Tags",SurfaceInfo.Tag));
foreach(string name in new[]{"Owner","ConfigFormatVersion","Surfaces"})keys.Add(Key(Settings,name));for(int i=0;i<
availableSurfaces;i++)foreach(string name in new[]{"Layout","Motion","Binding"})keys.Add(Key(Section(i),name));var metadata=new VRage.
Game.ModAPI.Ingame.Utilities.MyIni();var metadataKeys=new List<VRage.Game.ModAPI.Ingame.Utilities.MyIniKey>();if(setup&&
surfaceInfo!=null){if(!metadata.TryParse(surfaceInfo))return Reject("Invalid surface metadata.",out reason);metadata.GetKeys(
metadataKeys);keys.AddRange(metadataKeys);}CustomDataDocument document;if(!CustomDataDocument.TryParse(source,setup?keys.ToArray():
new VRage.Game.ModAPI.Ingame.Utilities.MyIniKey[0],new[]{SurfaceInfo.Tag},out document,out reason))return false;var
selection=BlockSelection.Evaluate(document,Key("Tags",SurfaceInfo.Tag),SurfaceInfo.Tag,out reason);if(selection==
BlockSelectionStatus.Invalid)return false;if(selection==BlockSelectionStatus.NotSelected)return true;if(availableSurfaces<1)return Reject(
"No text surfaces available.",out reason);string value;long parsedOwner;bool present=document.TryGet(Key(Settings,"Owner"),out value);if((!present&&!
setup)||(present&&(!PositiveInteger(value,out parsedOwner)||parsedOwner!=owner)))return Reject(
"Owner is invalid or belongs to another PB.",out reason);long version;present=document.TryGet(Key(Settings,"ConfigFormatVersion"),out value);if((!present&&!setup)||
(present&&(!PositiveInteger(value,out version)||version!=1)))return Reject("ConfigFormatVersion must be 1.",out reason);
var surfaces=new List<int>();if(!document.TryGet(Key(Settings,"Surfaces"),out value)){if(!setup)return Reject(
"Surfaces is required. Run setup first.",out reason);value="0";}foreach(string item in value.Split(',')){long index;if(!NonnegativeInteger(item,out index)||
index>=availableSurfaces||surfaces.Contains((int)index))return Reject("Invalid or duplicate surface index.",out reason);
surfaces.Add((int)index);}if(setup&&(!BlockSelection.PlanSetup(document,Key("Tags",SurfaceInfo.Tag),SurfaceInfo.Tag,out reason)
||!document.TrySeedMissing(Key(Settings,"Owner"),owner.ToString(System.Globalization.CultureInfo.InvariantCulture),null,
out reason)||!document.TrySeedMissing(Key(Settings,"ConfigFormatVersion"),"1",null,out reason)||!document.TrySeedMissing(
Key(Settings,"Surfaces"),"0",null,out reason)))return false;foreach(var key in metadataKeys)if(!document.TrySetOwned(key,
metadata.Get(key).ToString(),out reason))return false;plan=new SetupPlan{Document=document,SurfaceCount=surfaces.Count,Layouts=
new Dictionary<int,List<LayoutElement>>(),Motions=new Dictionary<int,MotionSet>(),Bindings=new Dictionary<int,BindingSet>()
,Surfaces=surfaces,Keys=keys,Source=source,Setup=setup};return true;}public static bool TryContinuePlan(SetupPlan plan,
out string reason){reason="";if(plan==null||plan.Complete)return Reject("No pending configuration plan.",out reason);var
document=plan.Document;bool setup=plan.Setup;if(plan.ParseStep<plan.Surfaces.Count*3){int index=plan.Surfaces[plan.ParseStep/3];
string name=new[]{"Layout","Motion","Binding"}[plan.ParseStep%3];var key=Key(Section(index),name);string value;bool present=
document.TryGet(key,out value);if(name=="Layout"){if(!present&&!setup)return Reject("Layout is required. Run setup first.",out
reason);if(!present)value="";List<LayoutElement>elements;if(!StaticLayout.TryParse(value,out elements,out reason)){reason=
"Surface "+index.ToString(System.Globalization.CultureInfo.InvariantCulture)+": "+reason;return false;}plan.Layouts.Add(index,
elements);}else if(name=="Motion"){MotionSet motion;if(!Motion.TryParse(value??"",plan.Layouts[index],out motion,out reason)){
reason="Surface "+index+": "+reason;return false;}plan.Motions.Add(index,motion);}else{BindingSet binding;if(!Binding.TryParse
(value??"",plan.Layouts[index],plan.Motions[index],out binding,out reason)){reason="Surface "+index+": "+reason;return
false;}plan.Bindings.Add(index,binding);}if(setup&&!document.TrySeedMissing(key,"",null,out reason))return false;plan.
ParseStep++;return true;}string candidate;if(setup){if(!document.TryBuild(out candidate,out reason))return false;}else candidate=
WithoutBareTags(plan.Source);if(candidate.Length>SurfaceInfo.MaxCustomData)return Reject("Setup output exceeds 32000 characters.",out
reason);var ini=new VRage.Game.ModAPI.Ingame.Utilities.MyIni();if(!ini.TryParse(candidate))return Reject(
"Invalid setup candidate.",out reason);var candidateKeys=new List<VRage.Game.ModAPI.Ingame.Utilities.MyIniKey>();ini.GetKeys(candidateKeys);
foreach(var key in candidateKeys){bool active=string.Equals(key.Section,Settings,StringComparison.OrdinalIgnoreCase);foreach(
int index in plan.Surfaces)active|=string.Equals(key.Section,Section(index),StringComparison.OrdinalIgnoreCase);if(active&&
!plan.Keys.Contains(key))return Reject("Unknown key in an active settings section.",out reason);}plan.Complete=true;plan.
Source=null;plan.Keys=null;plan.Surfaces=null;return true;}static string WithoutBareTags(string source){var text=new System.
Text.StringBuilder();bool tail=false;var lines=source.Split('\n');for(int i=0;i<lines.Length;i++){string line=lines[i];if(i>
0)text.Append('\n');if(line.Trim()=="---")tail=true;if(tail||line.Trim()!=SurfaceInfo.Tag)text.Append(line);}return text.
ToString();}public static bool TrySaveAll(List<SetupPlan>plans,out int saved,out string reason){saved=0;reason="";foreach(var
plan in plans){if(plan==null||!plan.Complete)return Reject("Configuration validation is incomplete.",out reason);string
candidate;if(!plan.Document.TryBuild(out candidate,out reason))return false;if(candidate.Length>SurfaceInfo.MaxCustomData)return
Reject("Setup output exceeds 32000 characters.",out reason);var status=plan.Document.TrySave(plan.Read,plan.Write,out reason);
if(status==CustomDataSaveStatus.Failed||status==CustomDataSaveStatus.Conflict)return false;if(status==CustomDataSaveStatus
.Saved)saved++;}return true;}static bool PositiveInteger(string value,out long number)=>NonnegativeInteger(value,out
number)&&number>0;static bool NonnegativeInteger(string value,out long number){number=0;if(value==null)return false;value=
value.Trim();if(value.Length==0)return false;foreach(char c in value)if(c<'0'||c>'9')return false;return long.TryParse(value,
System.Globalization.NumberStyles.None,System.Globalization.CultureInfo.InvariantCulture,out number);}static VRage.Game.ModAPI
.Ingame.Utilities.MyIniKey Key(string section,string name)=>new VRage.Game.ModAPI.Ingame.Utilities.MyIniKey(section,name)
;static string Section(int index)=>Settings+".Surface."+index.ToString(System.Globalization.CultureInfo.InvariantCulture)
;static bool Reject(string message,out string reason){reason=message;return false;}}public sealed class LayoutElement{
public string Id,Data,FontId="Debug",Alignment="CENTER";public bool IsText;public VRageMath.Vector2 Position,Size;public
VRageMath.Color?Color;public float AngleDegrees,TextScale=1;}public static class StaticLayout{public const int MaxLine=2048,
MaxData=256;internal sealed class Attribute{public string Value;public bool Quoted;}public static bool TryParse(string source,
out List<LayoutElement>elements,out string reason){elements=null;reason="";if(source==null||source.Length>SurfaceInfo.
MaxCustomData)return Reject("Invalid Layout input.",out reason);var result=new List<LayoutElement>();var ids=new HashSet<string>(
StringComparer.Ordinal);int lineNumber=0;foreach(string raw in source.Split('\n')){lineNumber++;string line;if(!StaticLayout.TryLine(
raw,out line))return LineError(lineNumber,"Invalid physical line or more than 2048 characters.",out reason);if(string.
IsNullOrWhiteSpace(line))continue;if(result.Count>=SurfacePass.MaxElements)return LineError(lineNumber,"Surface element limit exceeded.",
out reason);Dictionary<string,Attribute>attributes;LayoutElement element;string detail;if(!TryAttributes(line,out
attributes,out detail)||!TryElement(attributes,out element,out detail))return LineError(lineNumber,detail,out reason);if(!ids.Add(
element.Id))return LineError(lineNumber,"Duplicate element Id.",out reason);result.Add(element);}elements=result;return true;}
internal static bool TryLine(string raw,out string line){line=raw.EndsWith("\r",StringComparison.Ordinal)?raw.Substring(0,raw.
Length-1):raw;return line.Length<=MaxLine&&line.IndexOf('\r')<0;}internal static bool TryAttributes(string line,out Dictionary
<string,Attribute>attributes,out string reason){attributes=new Dictionary<string,Attribute>(StringComparer.Ordinal);
reason="";int position=0;while(position<line.Length){int start=position;while(position<line.Length&&line[position]!='='&&line[
position]!=';')position++;if(position==line.Length||line[position]!='=')return Reject("Expected attribute=value.",out reason);
string name=line.Substring(start,position-start).Trim();if(name.Length==0||attributes.ContainsKey(name))return Reject(
"Empty or duplicate attribute.",out reason);position++;while(position<line.Length&&char.IsWhiteSpace(line[position]))position++;bool quoted=position<
line.Length&&line[position]=='"';string value;if(quoted){position++;var text=new System.Text.StringBuilder();bool closed=
false;while(position<line.Length){char c=line[position++];if(c=='"'){closed=true;break;}if(c=='\\'){if(position==line.Length)
return Reject("Incomplete escape.",out reason);c=line[position++];switch(c){case'"':case'\\':break;case'n':c='\n';break;case
'r':c='\r';break;case't':c='\t';break;default:return Reject("Unsupported escape.",out reason);}}text.Append(c);}if(!closed)
return Reject("Unclosed string.",out reason);value=text.ToString();while(position<line.Length&&char.IsWhiteSpace(line[position
]))position++;if(position<line.Length&&line[position]!=';')return Reject("Unexpected text after string.",out reason);}
else{start=position;while(position<line.Length&&line[position]!=';')position++;value=line.Substring(start,position-start).
Trim();if(value.IndexOf('"')>=0)return Reject("Unexpected quote in unquoted value.",out reason);}attributes.Add(name,new
Attribute{Value=value,Quoted=quoted});if(position<line.Length)position++;int remainder=position;while(remainder<line.Length&&char
.IsWhiteSpace(line[remainder]))remainder++;if(remainder==line.Length)break;}return true;}static bool TryElement(
Dictionary<string,Attribute>attributes,out LayoutElement element,out string reason){element=null;reason="";Attribute id,type,data;
if(!attributes.TryGetValue("Id",out id)||!attributes.TryGetValue("Type",out type)||!attributes.TryGetValue("Data",out data
))return Reject("Id, Type and Data are required.",out reason);if(id.Quoted||!ValidId(id.Value))return Reject(
"Invalid Id.",out reason);if(type.Quoted||(type.Value!="TEXT"&&type.Value!="TEXTURE"))return Reject("Unsupported Type.",out reason);
bool isText=type.Value=="TEXT";if(!data.Quoted||data.Value.Length>MaxData||(!isText&&data.Value.Length==0))return Reject(
"Data must be a quoted string of at most 256 characters.",out reason);var result=new LayoutElement{Id=id.Value,IsText=isText,Data=data.Value};bool hasSize=false;foreach(var pair
in attributes){string name=pair.Key;var attribute=pair.Value;string value=attribute.Value;if(name=="Id"||name=="Type"||
name=="Data")continue;if(attribute.Quoted!=(name=="FontId"))return Reject("Incorrect attribute value type.",out reason);
switch(name){case"Position":if(!Vector(value,out result.Position))return Reject("Invalid Position.",out reason);break;case
"Size":if(isText||!Vector(value,out result.Size))return Reject("Invalid Size or Size on TEXT.",out reason);hasSize=true;break;
case"AngleDegrees":if(isText||!Number(value,out result.AngleDegrees))return Reject("Invalid AngleDegrees or angle on TEXT.",
out reason);break;case"TextScale":if(!isText||!Number(value,out result.TextScale)||result.TextScale<=0)return Reject(
"TextScale must be positive on TEXT.",out reason);break;case"FontId":if(!isText||value.Length==0)return Reject("FontId must be nonempty on TEXT.",out reason)
;result.FontId=value;break;case"Alignment":if(value!="CENTER"&&(!isText||(value!="LEFT"&&value!="RIGHT")))return Reject(
"Invalid Alignment.",out reason);result.Alignment=value;break;case"Color":VRageMath.Color color;if(!TryColor(value,out color))return Reject(
"Color requires four integers from 0 through 255.",out reason);result.Color=color;break;default:return Reject("Unknown Layout attribute.",out reason);}}if(!isText&&!
hasSize)return Reject("Size is required on TEXTURE.",out reason);element=result;return true;}static bool ValidId(string value){
if(value.Length<1||value.Length>32||!Letter(value[0]))return false;foreach(char c in value)if(!Letter(c)&&(c<'0'||c>'9')&&
c!='_')return false;return true;}static bool Letter(char c)=>(c>='A'&&c<='Z')||(c>='a'&&c<='z');static bool Number(string
value,out float number)=>float.TryParse(value,System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.
InvariantCulture,out number)&&!float.IsNaN(number)&&!float.IsInfinity(number);internal static bool Vector(string value,out VRageMath.
Vector2 vector){vector=default(VRageMath.Vector2);var parts=value.Split(',');float x,y;if(parts.Length!=2||!Number(parts[0],out
x)||!Number(parts[1],out y))return false;vector=new VRageMath.Vector2(x,y);return true;}internal static bool TryColor(
string value,out VRageMath.Color color){color=default(VRageMath.Color);var parts=value.Split(',');if(parts.Length!=4)return
false;var components=new int[4];for(int i=0;i<4;i++)if(!int.TryParse(parts[i],System.Globalization.NumberStyles.Integer,
System.Globalization.CultureInfo.InvariantCulture,out components[i])||components[i]<0||components[i]>255)return false;color=
new VRageMath.Color(components[0],components[1],components[2],components[3]);return true;}static bool LineError(int line,
string detail,out string reason)=>Reject("Layout line "+line.ToString(System.Globalization.CultureInfo.InvariantCulture)+": "+
detail,out reason);static bool Reject(string message,out string reason){reason=message;return false;}}public sealed class
DrawTarget{public long BlockId;public int Index;public List<SpriteDrawingElement>Elements;public List<LayoutElement>Layout;public
BindingSet Binding;public bool RefreshPending=true;}public static class DrawingData{public static List<SpriteDrawingElement>Build(
List<LayoutElement>elements){var result=new List<SpriteDrawingElement>();foreach(var element in elements)result.Add(new
SpriteDrawingElement{Kind=element.IsText?SpriteDrawingKind.Text:SpriteDrawingKind.Texture,Data=element.Data,Position=element.Position,Size=
element.IsText?(VRageMath.Vector2?)null:element.Size,FontId=element.IsText?element.FontId:null,Color=element.Color,Alignment=
element.Alignment=="LEFT"?VRage.Game.GUI.TextPanel.TextAlignment.LEFT:element.Alignment=="RIGHT"?VRage.Game.GUI.TextPanel.
TextAlignment.RIGHT:VRage.Game.GUI.TextPanel.TextAlignment.CENTER,RotationOrScale=element.IsText?element.TextScale:(float)((double)
element.AngleDegrees*Math.PI/180)});return result;}}public sealed class SurfacePass{public const int MaxElements=32;const int
BaseReserve=10000,ElementReserve=600;public int Index{get;private set;}public bool Pending{get;private set;}public double Time{get;
private set;}bool sampled;public void Begin(){Index=0;Pending=true;sampled=false;Time=0;}public double Sample(double elapsed){
if(!sampled){Time=elapsed;sampled=true;}return Time;}public bool TryContinue(bool periodic){if(!Pending&&periodic)Begin();
return Pending;}public void Advance(){Index++;}public void Cancel(){Index=0;Pending=false;sampled=false;Time=0;}public static
int RequiredInstructions(int elements)=>BaseReserve+ElementReserve*elements;public static bool CanStart(int used,int
maximum,int elements){if(used<0||elements<0||elements>MaxElements)return false;int reserve=RequiredInstructions(elements);
return used<=maximum-reserve;}}public sealed class RunStateStore{const string Header=
"LCDSpriteDrawer\nStateVersion=1\nRunning=";public bool Usable{get;private set;}public bool Running{get;private set;}public bool StatusColorsInitialized{get;
private set;}public bool Load(string source,out string reason){Usable=false;Running=false;StatusColorsInitialized=false;reason=
"";if(source==""){Usable=true;return true;}if(source==null||source.Length>256){reason=
"Invalid saved run state. Use stop to reset private Storage.";return false;}string value=source.Replace("\r\n","\n");const string colors="StatusColorsInitialized=true\n";bool
initialized=value.EndsWith(colors,StringComparison.Ordinal);if(initialized)value=value.Substring(0,value.Length-colors.Length);if(
value!=Encode(false)&&value!=Encode(true)){reason=
"Unsupported or corrupt saved run state. Use stop to reset private Storage.";return false;}Running=value==Encode(true);StatusColorsInitialized=initialized;Usable=true;return true;}public bool
InitializeStatusColors(Func<string>read,Action<string>write,out string reason){if(!Usable){reason="Saved run state is locked.";return false;}
StatusColorsInitialized=true;return Commit(Running,read,write,out reason);}public bool Commit(bool running,Func<string>read,Action<string>write
,out string reason,bool reset=false){reason="";if(!Usable&&!reset){reason=
"Saved run state is locked. Use stop to reset private Storage.";return false;}if(reset&&running){reason="Only Stopped can reset private Storage.";return false;}try{string candidate=
Encode(running)+(StatusColorsInitialized?"StatusColorsInitialized=true\n":"");if(read()!=candidate){write(candidate);if(read()
!=candidate)throw new Exception();}Running=running;Usable=true;return true;}catch(Exception){Running=false;Usable=false;
reason="Run state save failed; previous Storage may remain. Use stop before retrying.";return false;}}public static string
Encode(bool running)=>Header+(running?"true\n":"false\n");}public enum MotionProperty{MoveX,MoveY,Width,Height,Angle,TextScale
,Orbit}public enum MotionMode{Clamp,Loop,PingPong}public sealed class MotionTrack{public MotionProperty Property;public
MotionMode Mode;public double Rate,Initial,Min,Max;public bool Bounded;public Vector2 Anchor,Center;public MotionTrack Basis;
public string Combine;}public sealed class MotionSet{public List<MotionTrack>[]Elements;public int Count;}public static class
Motion{public static bool TryParse(string source,List<LayoutElement>layout,out MotionSet result,out string reason){result=null
;reason="";if(source==null||source.Length>SurfaceInfo.MaxCustomData||layout==null)return Reject("Invalid Motion input.",
out reason);var plan=new MotionSet{Elements=new List<MotionTrack>[layout.Count]};var ids=new Dictionary<string,int>(
StringComparer.Ordinal);for(int i=0;i<layout.Count;i++){ids.Add(layout[i].Id,i);plan.Elements[i]=new List<MotionTrack>();}int
lineNumber=0;foreach(string raw in source.Split('\n')){lineNumber++;string line;if(!StaticLayout.TryLine(raw,out line))return
LineError(lineNumber,"Invalid physical line or more than 2048 characters.",out reason);if(string.IsNullOrWhiteSpace(line))
continue;Dictionary<string,StaticLayout.Attribute>attributes;string detail;int index;MotionTrack track;if(!StaticLayout.
TryAttributes(line,out attributes,out detail)||!TryTrack(attributes,layout,ids,out index,out track,out detail))return LineError(
lineNumber,detail,out reason);foreach(var existing in plan.Elements[index]){if(existing.Property==track.Property)return LineError(
lineNumber,"Duplicate element/property.",out reason);if(Dimension(existing.Property)&&Dimension(track.Property)&&existing.Anchor!=
track.Anchor)return LineError(lineNumber,"Dimension anchors must match.",out reason);}plan.Elements[index].Add(track);plan.
Count++;}List<SpriteDrawingElement>initial;if(plan.Count>0&&!TryBuild(layout,plan,0,out initial,out reason))return false;
result=plan;return true;}static bool TryTrack(Dictionary<string,StaticLayout.Attribute>attributes,List<LayoutElement>layout,
Dictionary<string,int>ids,out int index,out MotionTrack track,out string reason){index=0;track=null;reason="";StaticLayout.
Attribute element,property,rate;if(!attributes.TryGetValue("Element",out element)||!attributes.TryGetValue("Property",out
property)||!attributes.TryGetValue("Rate",out rate)||element.Quoted||!ids.TryGetValue(element.Value,out index))return Reject(
"Element, Property and Rate are required; Element must exist.",out reason);foreach(var pair in attributes)if(pair.Value.Quoted||(pair.Key!="Element"&&pair.Key!="Property"&&pair.Key!=
"Rate"&&pair.Key!="Mode"&&pair.Key!="Min"&&pair.Key!="Max"&&pair.Key!="Initial"&&pair.Key!="Anchor"&&pair.Key!="Center"))
return Reject("Unknown or quoted Motion attribute.",out reason);MotionProperty kind;if(!Enum.TryParse(property.Value,out kind)
||(int)kind<0||(int)kind>(int)MotionProperty.Orbit||kind.ToString()!=property.Value)return Reject(
"Invalid Motion Property.",out reason);var baseline=layout[index];if(!Applies(kind,baseline.IsText))return Reject(
"Motion Property does not apply to this Type.",out reason);var value=new MotionTrack{Property=kind};if(!Number(rate.Value,out value.Rate))return Reject(
"Invalid Motion Rate.",out reason);value.Initial=kind==MotionProperty.Width?baseline.Size.X:kind==MotionProperty.Height?baseline.Size.Y:kind==
MotionProperty.TextScale?baseline.TextScale:0;StaticLayout.Attribute input;if(attributes.TryGetValue("Initial",out input)&&!Number(
input.Value,out value.Initial))return Reject("Invalid Initial.",out reason);if(attributes.TryGetValue("Mode",out input)&&(!
Enum.TryParse(input.Value,out value.Mode)||(int)value.Mode<0||(int)value.Mode>(int)MotionMode.PingPong||value.Mode.ToString(
)!=input.Value))return Reject("Invalid Motion Mode.",out reason);bool minimum=attributes.TryGetValue("Min",out input);if(
minimum&&!Number(input.Value,out value.Min))return Reject("Invalid Min.",out reason);bool maximum=attributes.TryGetValue("Max",
out input);if(maximum&&!Number(input.Value,out value.Max))return Reject("Invalid Max.",out reason);value.Bounded=minimum&&
maximum;if(minimum!=maximum||(value.Mode!=MotionMode.Clamp&&!value.Bounded)||(value.Bounded&&(!(value.Min<value.Max)||!Finite(
value.Max-value.Min)||value.Initial<value.Min||value.Initial>value.Max)))return Reject("Invalid Motion bounds or Initial.",
out reason);if(kind==MotionProperty.TextScale&&(value.Initial<=0||(value.Bounded&&value.Min<=0)))return Reject(
"TextScale values and bounds must be positive.",out reason);if(attributes.TryGetValue("Anchor",out input)&&!TryAnchor(input.Value,out value.Anchor))return Reject(
"Invalid Anchor.",out reason);bool center=attributes.TryGetValue("Center",out input);if(center!=(kind==MotionProperty.Orbit)||(center&&!
StaticLayout.Vector(input.Value,out value.Center)))return Reject("Center is required only for Orbit.",out reason);track=value;return
true;}internal static bool TryAnchor(string value,out Vector2 anchor){string[]names={"TopLeft","TopCenter","TopRight",
"CenterLeft","Center","CenterRight","BottomLeft","BottomCenter","BottomRight"};for(int i=0;i<names.Length;i++)if(value==names[i]){
anchor=new Vector2((i%3-1)*0.5f,(i/3-1)*0.5f);return true;}anchor=Vector2.Zero;return false;}public static bool TryValue(
MotionTrack track,double time,out double value){if(track.Basis!=null){value=track.Initial;if(!Finite(time)||time<0||!Finite(value))
return false;if(track.Combine!="Replace"){double basis;if(!TryValue(track.Basis,time,out basis))return false;value=track.
Combine=="Add"?basis+value:basis*value;}return Finite(value)&&(track.Property!=MotionProperty.TextScale||value>0);}value=track.
Initial+track.Rate*time;if(!Finite(time)||time<0||!Finite(value))return false;if(track.Mode==MotionMode.Clamp){if(track.Bounded
)value=Math.Max(track.Min,Math.Min(track.Max,value));}else{double width=track.Max-track.Min,period=track.Mode==MotionMode
.Loop?width:2*width;double z=track.Initial-track.Min+track.Rate*time;if(!Finite(width)||width<=0||!Finite(period)||!
Finite(z))return false;double r=z-period*Math.Floor(z/period);value=track.Min+(track.Mode==MotionMode.Loop||r<=width?r:2*width
-r);}return Finite(value)&&(track.Property!=MotionProperty.TextScale||value>0);}public static bool TryBuild(List<
LayoutElement>layout,MotionSet motion,double time,out List<SpriteDrawingElement>result,out string reason){result=null;reason="";var
sprites=DrawingData.Build(layout);for(int i=0;i<layout.Count;i++){if(motion.Elements[i].Count==0)continue;var baseline=layout[i
];double dx=0,dy=0,width=baseline.Size.X,height=baseline.Size.Y;double angle=0,orbit=0,textScale=baseline.TextScale;
Vector2 anchor=Vector2.Zero,angleAnchor=Vector2.Zero,center=Vector2.Zero;bool dimension=false;foreach(var track in motion.
Elements[i]){double value;if(!TryValue(track,time,out value))return Reject("Motion value is non-finite or invalid. Element="+
baseline.Id,out reason);switch(track.Property){case MotionProperty.MoveX:dx=value;break;case MotionProperty.MoveY:dy=value;break
;case MotionProperty.Width:width=value;anchor=track.Anchor;dimension=true;break;case MotionProperty.Height:height=value;
anchor=track.Anchor;dimension=true;break;case MotionProperty.Angle:angle=value;angleAnchor=track.Anchor;break;case
MotionProperty.TextScale:textScale=value;break;case MotionProperty.Orbit:orbit=value;center=track.Center;break;}}if(!dimension)anchor=
angleAnchor;const double radians=Math.PI/180;double a0=baseline.AngleDegrees*radians,a1=a0+angle*radians,b=orbit*radians;if(!Finite
(a1)||!Finite(b))return Reject("Motion angle overflow. Element="+baseline.Id,out reason);double x=baseline.Position.X,y=
baseline.Position.Y;if(anchor!=Vector2.Zero){double c0=a0==0?1:Math.Cos(a0),s0=a0==0?0:Math.Sin(a0);double c1=a1==0?1:Math.Cos(
a1),s1=a1==0?0:Math.Sin(a1);double oldX=anchor.X*baseline.Size.X,oldY=anchor.Y*baseline.Size.Y;double newX=anchor.X*width,
newY=anchor.Y*height;x+=c0*oldX-s0*oldY-c1*newX+s1*newY;y+=s0*oldX+c0*oldY-s1*newX-c1*newY;}if(b!=0){double cb=Math.Cos(b),
sb=Math.Sin(b),relativeX=x-center.X,relativeY=y-center.Y;x=center.X+cb*relativeX-sb*relativeY;y=center.Y+sb*relativeX+cb*
relativeY;}var sprite=sprites[i];sprite.Position=new Vector2((float)(x+dx),(float)(y+dy));sprite.Size=baseline.IsText?(Vector2?)
null:new Vector2((float)width,(float)height);sprite.RotationOrScale=(float)(baseline.IsText?textScale:a1);if(!Finite(sprite.
Position.Value.X)||!Finite(sprite.Position.Value.Y)||(!baseline.IsText&&(!Finite(sprite.Size.Value.X)||!Finite(sprite.Size.Value
.Y)))||!Finite(sprite.RotationOrScale.Value)||(baseline.IsText&&sprite.RotationOrScale.Value<=0))return Reject(
"Motion API value overflow or invalid TextScale. Element="+baseline.Id,out reason);sprites[i]=sprite;}result=sprites;return true;}public static bool TryAdvance(double current,
double delta,out double elapsed){elapsed=current+delta;return Finite(current)&&current>=0&&Finite(delta)&&delta>=0&&Finite(
elapsed);}internal static bool Applies(MotionProperty property,bool text)=>!(text&&(Dimension(property)||property==
MotionProperty.Angle))&&(text||property!=MotionProperty.TextScale);internal static bool Dimension(MotionProperty property)=>property==
MotionProperty.Width||property==MotionProperty.Height;internal static bool Number(string source,out double value)=>double.TryParse(
source,System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out value)&&Finite(value);
internal static bool Finite(double value)=>!double.IsNaN(value)&&!double.IsInfinity(value);static bool LineError(int line,string
detail,out string reason)=>Reject("Motion line "+line+": "+detail,out reason);static bool Reject(string message,out string
reason){reason=message;return false;}}public sealed class BindingInput{public long BlockId;public BlockValueKind Source;public
double Value;public bool Enabled,Lost;public int Users;}public sealed class BindingTrack{public int Element,Decimals=1;public
string Property,BlockName,Prefix="",Suffix="",Combine;public BlockValueKind Source;public MotionMode Mode;public double
InputMin,InputMax=1,OutputMin,OutputMax;public Color MinColor,MaxColor;public double[]Thresholds;public string[]Cases;public
MotionTrack Numeric;public BindingInput Input;}public sealed class BindingSet{public List<BindingTrack>Tracks=new List<BindingTrack
>();public MotionSet Combined;public HashSet<BindingInput>Inputs=new HashSet<BindingInput>();public bool SourceLost{get{
foreach(var input in Inputs)if(input.Lost)return true;return false;}}}public static class Binding{public static void IndexName(
Dictionary<string,long>names,string name,long id){if(names.ContainsKey(name))names[name]=0;else names.Add(name,id);}public static
long FindName(Dictionary<string,long>names,string name){long id;return names.TryGetValue(name,out id)?id:0;}public static
bool TryParse(string source,List<LayoutElement>layout,MotionSet motion,out BindingSet result,out string reason){result=null;
reason="";if(source==null||source.Length>SurfaceInfo.MaxCustomData||layout==null||motion==null)return Reject(
"Invalid Binding input.",out reason);if(source.Length==0){result=new BindingSet{Combined=motion};return true;}var plan=new BindingSet{Combined=
new MotionSet{Elements=new List<MotionTrack>[layout.Count],Count=motion.Count}};var ids=new Dictionary<string,int>(
StringComparer.Ordinal);var properties=new HashSet<string>(StringComparer.Ordinal);for(int i=0;i<layout.Count;i++){ids.Add(layout[i].
Id,i);plan.Combined.Elements[i]=new List<MotionTrack>(motion.Elements[i]);}int lineNumber=0;foreach(string raw in source.
Split('\n')){lineNumber++;string line;if(!StaticLayout.TryLine(raw,out line))return Reject("Binding line "+lineNumber+
": Invalid physical line.",out reason);if(string.IsNullOrWhiteSpace(line))continue;Dictionary<string,StaticLayout.Attribute>attributes;
BindingTrack track;if(!StaticLayout.TryAttributes(line,out attributes,out reason)||!TryTrack(attributes,ids,layout,out track,out
reason)){reason="Binding line "+lineNumber+": "+reason;return false;}if(!properties.Add(track.Element+":"+track.Property))
return Reject("Duplicate Binding element/property.",out reason);if(track.Numeric!=null){var tracks=plan.Combined.Elements[
track.Element];int duplicate=-1;for(int i=0;i<tracks.Count;i++){var previous=tracks[i];if(previous.Property==track.Numeric.
Property)duplicate=i;if(Motion.Dimension(previous.Property)&&Motion.Dimension(track.Numeric.Property)&&previous.Anchor!=track.
Numeric.Anchor)return Reject("Dimension anchors must match.",out reason);}if(duplicate>=0){var previous=tracks[duplicate];if(
track.Combine==null)return Reject("Duplicate Motion/Binding property requires Combine.",out reason);if(previous.Anchor!=track
.Numeric.Anchor||(track.Numeric.Property==MotionProperty.Orbit&&track.Combine!="Replace"&&previous.Center!=track.Numeric.
Center))return Reject("Combined anchors and orbit centers must match.",out reason);track.Numeric.Basis=previous;track.Numeric.
Combine=track.Combine;tracks[duplicate]=track.Numeric;}else{if(track.Combine!=null)return Reject(
"Combine requires matching Motion.",out reason);tracks.Add(track.Numeric);plan.Combined.Count++;}}plan.Tracks.Add(track);}result=plan;return true;}static
bool TryTrack(Dictionary<string,StaticLayout.Attribute>attributes,Dictionary<string,int>ids,List<LayoutElement>layout,out
BindingTrack result,out string reason){result=null;reason="";var track=new BindingTrack();string element,property,source,name,value;
if(!Take(attributes,"Element",false,out element)||!ids.TryGetValue(element,out track.Element)||!Take(attributes,"Property"
,false,out property)||!Take(attributes,"Source",false,out source)||!Take(attributes,"BlockName",true,out name)||name.
Length==0||!BlockValues.TryKind(source,out track.Source))return Reject(
"Element, Property, Source and quoted BlockName are required.",out reason);track.Property=property;track.BlockName=name;var baseline=layout[track.Element];bool text=property=="Data"
&&baseline.IsText,color=property=="Color",visible=property=="Visible",data=property=="Data"&&!baseline.IsText;
MotionProperty numeric;if(!text&&!color&&!visible&&!data){if(!Enum.TryParse(property,out numeric)||(int)numeric<0||(int)numeric>(int)
MotionProperty.Orbit||numeric.ToString()!=property||!Motion.Applies(numeric,baseline.IsText))return Reject(
"Invalid Binding Property for Type.",out reason);track.Numeric=new MotionTrack{Property=numeric};if(attributes.ContainsKey("Combine")){if(!Take(attributes,
"Combine",false,out track.Combine)||(track.Combine!="Replace"&&track.Combine!="Add"&&track.Combine!="Multiply"))return Reject(
"Invalid Combine.",out reason);}if(!Number(attributes,"OutputMin",out track.OutputMin)||!Number(attributes,"OutputMax",out track.OutputMax
)||!Motion.Finite(track.OutputMax-track.OutputMin))return Reject("Invalid Binding output range.",out reason);if(numeric==
MotionProperty.TextScale&&track.Combine!="Add"&&(!PositiveScale(track.OutputMin)||!PositiveScale(track.OutputMax)))return Reject(
"TextScale endpoints must be positive finite API values.",out reason);track.Numeric.Initial=track.OutputMin;if(numeric==MotionProperty.Orbit&&(!Take(attributes,"Center",false,
out value)||!StaticLayout.Vector(value,out track.Numeric.Center)))return Reject("Center is required for Orbit.",out reason)
;if(attributes.ContainsKey("Anchor")&&Motion.Dimension(numeric)&&(!Take(attributes,"Anchor",false,out value)||!Motion.
TryAnchor(value,out track.Numeric.Anchor)))return Reject("Invalid Anchor.",out reason);}if(color&&(!ReadColor(attributes,
"MinColor",out track.MinColor)||!ReadColor(attributes,"MaxColor",out track.MaxColor)))return Reject(
"MinColor/MaxColor require RGBA integers.",out reason);if(track.Numeric!=null||color){if(!BlockValues.IsBoolean(track.Source)&&(!Number(attributes,"InputMin",out
track.InputMin)||!Number(attributes,"InputMax",out track.InputMax)||track.InputMin>=track.InputMax||!Motion.Finite(track.
InputMax-track.InputMin)))return Reject("Invalid Binding input range.",out reason);if(attributes.ContainsKey("Mode")&&(!Take(
attributes,"Mode",false,out value)||!Enum.TryParse(value,out track.Mode)||(int)track.Mode<0||(int)track.Mode>(int)MotionMode.
PingPong||track.Mode.ToString()!=value))return Reject("Invalid Binding Mode.",out reason);if(BlockValues.IsBoolean(track.Source)
&&track.Mode!=MotionMode.Clamp)return Reject("Boolean mapping uses Clamp.",out reason);}else if(data||visible||attributes.
ContainsKey("Thresholds")){if(!Take(attributes,"Thresholds",false,out value))return Reject("Thresholds are required for switching."
,out reason);var parts=value.Split(',');track.Thresholds=new double[parts.Length];track.Cases=new string[parts.Length+1];
for(int i=0;i<parts.Length;i++)if(!Motion.Number(parts[i],out track.Thresholds[i])||(i>0&&track.Thresholds[i]<=track.
Thresholds[i-1]))return Reject("Thresholds must be finite and strictly increasing.",out reason);for(int i=0;i<track.Cases.Length;i
++){if(!Take(attributes,"Case"+i,!visible,out value)||(visible?value!="true"&&value!="false":value.Length>StaticLayout.
MaxData||(data&&value.Length==0)))return Reject("Invalid or missing switch Case.",out reason);track.Cases[i]=value;}}else if(
text){if(attributes.ContainsKey("Decimals")){if(!Take(attributes,"Decimals",false,out value)||value.Length!=1||value[0]<'0'
||value[0]>'3')return Reject("Decimals must be 0 through 3.",out reason);track.Decimals=value[0]-'0';}if(attributes.
ContainsKey("Prefix")&&(!Take(attributes,"Prefix",true,out track.Prefix)||track.Prefix.Length>StaticLayout.MaxData))return Reject(
"Invalid Prefix.",out reason);if(attributes.ContainsKey("Suffix")&&(!Take(attributes,"Suffix",true,out track.Suffix)||track.Suffix.Length
>StaticLayout.MaxData))return Reject("Invalid Suffix.",out reason);}if(attributes.Count!=0)return Reject(
"Unknown or inapplicable Binding attribute.",out reason);result=track;return true;}static bool TryUnit(BindingTrack track,double value,out double unit){unit=0;if(!
Motion.Finite(value))return false;if(track.Mode==MotionMode.Clamp)unit=value<=track.InputMin?0:value>=track.InputMax?1:(value-
track.InputMin)/(track.InputMax-track.InputMin);else{double z=(value-track.InputMin)/(track.InputMax-track.InputMin);if(!
Motion.Finite(z))return false;double period=track.Mode==MotionMode.Loop?1:2;unit=z-period*Math.Floor(z/period);if(track.Mode==
MotionMode.PingPong&&unit>1)unit=2-unit;}return Motion.Finite(unit)&&unit>=0&&unit<=1;}public static bool TryMap(BindingTrack
track,double value,out double result){result=0;double unit;if(!TryUnit(track,value,out unit))return false;result=unit==0?
track.OutputMin:unit==1?track.OutputMax:track.OutputMin+(track.OutputMax-track.OutputMin)*unit;return Motion.Finite(result)&&
(track.Numeric.Property!=MotionProperty.TextScale||track.Combine=="Add"||PositiveScale(result));}public static bool
TryText(BindingTrack track,double value,out string text){text=null;if(!Motion.Finite(value))return false;text=track.Prefix+
value.ToString("F"+track.Decimals,System.Globalization.CultureInfo.InvariantCulture)+track.Suffix;return text.Length<=
StaticLayout.MaxData;}public static bool TryBuild(List<LayoutElement>layout,BindingSet binding,double time,out List<
SpriteDrawingElement>elements,out string reason){elements=null;reason="";foreach(var track in binding.Tracks){if(track.Input==null)return
Reject("Binding reference was not resolved.",out reason);double value=InputValue(track);if(!Motion.Finite(value))return Reject
("Invalid Binding input value.",out reason);if(track.Numeric!=null){double mapped;if(!TryMap(track,value,out mapped))
return Reject("Invalid numeric mapping.",out reason);track.Numeric.Initial=mapped;}}if(!Motion.TryBuild(layout,binding.
Combined,time,out elements,out reason))return false;var hidden=new HashSet<int>();foreach(var track in binding.Tracks){var
sprite=elements[track.Element];double value=InputValue(track);if(track.Cases!=null){int index=0;while(index<track.Thresholds.
Length&&value>=track.Thresholds[index])index++;if(track.Property=="Visible"){if(track.Cases[index]=="false")hidden.Add(track.
Element);}else sprite.Data=track.Cases[index];}else if(track.Property=="Data"){string text;if(!TryText(track,value,out text)){
elements=null;return Reject("Invalid numeric text or more than 256 characters.",out reason);}sprite.Data=text;}else if(track.
Property=="Color"){double unit;if(!TryUnit(track,value,out unit)){elements=null;return Reject("Invalid color mapping.",out
reason);}sprite.Color=new Color(Channel(track.MinColor.R,track.MaxColor.R,unit),Channel(track.MinColor.G,track.MaxColor.G,unit
),Channel(track.MinColor.B,track.MaxColor.B,unit),Channel(track.MinColor.A,track.MaxColor.A,unit));}elements[track.
Element]=sprite;}for(int i=elements.Count-1;i>=0;i--)if(hidden.Contains(i))elements.RemoveAt(i);return true;}static int Channel
(byte low,byte high,double unit)=>(int)Math.Floor(low+(high-low)*unit+0.5);static double InputValue(BindingTrack track)=>
BlockValues.IsBoolean(track.Source)?(track.Input.Enabled?1:0):track.Input.Value;public static bool Matches(IMyTerminalBlock block,
IMyTerminalBlock me,BlockValueKind source)=>block!=null&&block.IsSameConstructAs(me)&&BlockValues.Accepts(block,source);static bool
PositiveScale(double value)=>value>0&&Motion.Finite(value)&&(float)value>0&&!float.IsInfinity((float)value);static bool Take(
Dictionary<string,StaticLayout.Attribute>attributes,string key,bool quoted,out string value){value=null;StaticLayout.Attribute
attribute;if(!attributes.TryGetValue(key,out attribute)||attribute.Quoted!=quoted)return false;value=attribute.Value;attributes.
Remove(key);return true;}static bool Number(Dictionary<string,StaticLayout.Attribute>attributes,string key,out double value){
value=0;string text;return Take(attributes,key,false,out text)&&Motion.Number(text,out value);}static bool ReadColor(
Dictionary<string,StaticLayout.Attribute>attributes,string key,out Color value){value=default(Color);string text;return Take(
attributes,key,false,out text)&&StaticLayout.TryColor(text,out value);}static bool Reject(string message,out string reason){reason
=message;return false;}}public enum BlockValueKind{Speed,Enabled,IsWorking,IsFunctional,PowerOutput,PowerMaxOutput,
BatteryStoredPower,BatteryMaxStoredPower,BatteryChargeRatio,BatteryInput,BatteryMaxInput,BatteryIsCharging,GasFillRatio,GasCapacity,
DoorOpenRatio,PistonPosition,PistonVelocity,RotorAngle,RotorVelocity,RotorLocked,ShipBaseMass,ShipTotalMass,ShipPhysicalMass,
NaturalGravity,ArtificialGravity,TotalGravity,AngularSpeed,IsUnderControl,HandBrake,Dampeners}public static class BlockValues{public
static bool TryKind(string text,out BlockValueKind kind)=>Enum.TryParse(text,out kind)&&kind>=BlockValueKind.Speed&&kind<=
BlockValueKind.Dampeners&&kind.ToString()==text;public static bool IsBoolean(BlockValueKind kind)=>kind==BlockValueKind.Enabled||kind
==BlockValueKind.IsWorking||kind==BlockValueKind.IsFunctional||kind==BlockValueKind.BatteryIsCharging||kind==
BlockValueKind.RotorLocked||kind==BlockValueKind.IsUnderControl||kind==BlockValueKind.HandBrake||kind==BlockValueKind.Dampeners;public
static string Key(BlockValueKind kind,string name)=>kind+":"+name;public static bool Accepts(IMyTerminalBlock block,
BlockValueKind kind){if(block==null)return false;switch(kind){case BlockValueKind.Enabled:return block is IMyFunctionalBlock;case
BlockValueKind.IsWorking:case BlockValueKind.IsFunctional:return true;case BlockValueKind.PowerOutput:case BlockValueKind.
PowerMaxOutput:return block is IMyPowerProducer;case BlockValueKind.BatteryStoredPower:case BlockValueKind.BatteryMaxStoredPower:case
BlockValueKind.BatteryChargeRatio:case BlockValueKind.BatteryInput:case BlockValueKind.BatteryMaxInput:case BlockValueKind.
BatteryIsCharging:return block is IMyBatteryBlock;case BlockValueKind.GasFillRatio:case BlockValueKind.GasCapacity:return block is
IMyGasTank;case BlockValueKind.DoorOpenRatio:return block is IMyDoor;case BlockValueKind.PistonPosition:case BlockValueKind.
PistonVelocity:return block is IMyPistonBase;case BlockValueKind.RotorAngle:case BlockValueKind.RotorVelocity:case BlockValueKind.
RotorLocked:return block is IMyMotorStator;case BlockValueKind.Speed:case BlockValueKind.ShipBaseMass:case BlockValueKind.
ShipTotalMass:case BlockValueKind.ShipPhysicalMass:case BlockValueKind.NaturalGravity:case BlockValueKind.ArtificialGravity:case
BlockValueKind.TotalGravity:case BlockValueKind.AngularSpeed:case BlockValueKind.IsUnderControl:case BlockValueKind.HandBrake:case
BlockValueKind.Dampeners:return block is IMyShipController;default:return false;}}public static bool TryRatio(double current,double
maximum,out double value){value=0;if(!Motion.Finite(current)||!Motion.Finite(maximum)||maximum<=0)return false;value=current/
maximum;return Motion.Finite(value);}public static bool TryRead(IMyTerminalBlock block,BlockValueKind kind,out double value,out
bool flag,out string reason){value=0;flag=false;reason="";if(!Accepts(block,kind))return Reject(
"Invalid block value source.",out reason);switch(kind){case BlockValueKind.Enabled:flag=((IMyFunctionalBlock)block).Enabled;break;case BlockValueKind
.IsWorking:flag=block.IsWorking;break;case BlockValueKind.IsFunctional:flag=block.IsFunctional;break;case BlockValueKind.
PowerOutput:value=((IMyPowerProducer)block).CurrentOutput;break;case BlockValueKind.PowerMaxOutput:value=((IMyPowerProducer)block).
MaxOutput;break;case BlockValueKind.BatteryStoredPower:value=((IMyBatteryBlock)block).CurrentStoredPower;break;case
BlockValueKind.BatteryMaxStoredPower:value=((IMyBatteryBlock)block).MaxStoredPower;break;case BlockValueKind.BatteryChargeRatio:var
battery=(IMyBatteryBlock)block;if(!TryRatio(battery.CurrentStoredPower,battery.MaxStoredPower,out value))return Reject(
"Invalid battery charge ratio.",out reason);break;case BlockValueKind.BatteryInput:value=((IMyBatteryBlock)block).CurrentInput;break;case
BlockValueKind.BatteryMaxInput:value=((IMyBatteryBlock)block).MaxInput;break;case BlockValueKind.BatteryIsCharging:flag=((
IMyBatteryBlock)block).IsCharging;break;case BlockValueKind.GasFillRatio:value=((IMyGasTank)block).FilledRatio;break;case
BlockValueKind.GasCapacity:value=((IMyGasTank)block).Capacity;break;case BlockValueKind.DoorOpenRatio:value=((IMyDoor)block).OpenRatio
;break;case BlockValueKind.PistonPosition:value=((IMyPistonBase)block).CurrentPosition;break;case BlockValueKind.
PistonVelocity:value=((IMyPistonBase)block).Velocity;break;case BlockValueKind.RotorAngle:value=((IMyMotorStator)block).Angle;break;
case BlockValueKind.RotorVelocity:value=((IMyMotorStator)block).TargetVelocityRPM;break;case BlockValueKind.RotorLocked:flag
=((IMyMotorStator)block).RotorLock;break;case BlockValueKind.Speed:value=((IMyShipController)block).GetShipSpeed();break;
case BlockValueKind.ShipBaseMass:value=((IMyShipController)block).CalculateShipMass().BaseMass;break;case BlockValueKind.
ShipTotalMass:value=((IMyShipController)block).CalculateShipMass().TotalMass;break;case BlockValueKind.ShipPhysicalMass:value=((
IMyShipController)block).CalculateShipMass().PhysicalMass;break;case BlockValueKind.NaturalGravity:value=((IMyShipController)block).
GetNaturalGravity().Length();break;case BlockValueKind.ArtificialGravity:value=((IMyShipController)block).GetArtificialGravity().Length()
;break;case BlockValueKind.TotalGravity:value=((IMyShipController)block).GetTotalGravity().Length();break;case
BlockValueKind.AngularSpeed:value=((IMyShipController)block).GetShipVelocities().AngularVelocity.Length();break;case BlockValueKind.
IsUnderControl:flag=((IMyShipController)block).IsUnderControl;break;case BlockValueKind.HandBrake:flag=((IMyShipController)block).
HandBrake;break;case BlockValueKind.Dampeners:flag=((IMyShipController)block).DampenersOverride;break;}return Motion.Finite(value
)||Reject("Block value is not finite.",out reason);}static bool Reject(string message,out string reason){reason=message;
return false;}}public enum CustomDataSaveStatus{Unchanged,Saved,Conflict,Failed}public sealed class CustomDataDocument{string
source,tail;VRage.Game.ModAPI.Ingame.Utilities.MyIni original,edited;HashSet<VRage.Game.ModAPI.Ingame.Utilities.MyIniKey>owned
;HashSet<string>allowed,consumed=new HashSet<string>(StringComparer.Ordinal);List<string>tokenLines;bool dirty,ended;
CustomDataDocument(string source,VRage.Game.ModAPI.Ingame.Utilities.MyIni original,VRage.Game.ModAPI.Ingame.Utilities.MyIni edited,HashSet
<VRage.Game.ModAPI.Ingame.Utilities.MyIniKey>owned,HashSet<string>allowed,List<string>tokenLines,string tail){this.source
=source;this.original=original;this.edited=edited;this.owned=owned;this.allowed=allowed;this.tokenLines=tokenLines;this.
tail=tail;}public static bool TryParse(string source,VRage.Game.ModAPI.Ingame.Utilities.MyIniKey[]ownedKeys,string[]
setupTokens,out CustomDataDocument document,out string reason){document=null;reason="";if(source==null||ownedKeys==null||
setupTokens==null)return Fail("Null document input.",out reason);var owned=new HashSet<VRage.Game.ModAPI.Ingame.Utilities.MyIniKey>
();foreach(var key in ownedKeys){if(key.IsEmpty)return Fail("Empty owned key.",out reason);owned.Add(key);}var allowed=
new HashSet<string>(StringComparer.Ordinal);foreach(var token in setupTokens){if(string.IsNullOrWhiteSpace(token)||token!=
token.Trim()||token.IndexOfAny(new[]{'\r','\n','=','[',']',';','|'})>=0||token=="---")return Fail("Invalid setup token.",out
reason);allowed.Add(token);}var tokens=new List<string>();var clean=new System.Text.StringBuilder();var sections=new HashSet<
string>(StringComparer.OrdinalIgnoreCase);var keys=new HashSet<VRage.Game.ModAPI.Ingame.Utilities.MyIniKey>();string section=
"",tail="";for(int start=0;start<source.Length;){int end=source.IndexOf('\n',start);if(end<0)end=source.Length;int next=
end<source.Length?end+1:end;string line=source.Substring(start,end-start).Trim();if(line=="---"){tail=source.Substring(
start);break;}if(allowed.Contains(line)){tokens.Add(source.Substring(start,next-start));clean.Append('\n');}else{if(line.
StartsWith("[",StringComparison.Ordinal)&&line.EndsWith("]",StringComparison.Ordinal)){section=line.Substring(1,line.Length-2);if(
!sections.Add(section))return Fail("Duplicate INI section.",out reason);}else if(line.Length>0&&line[0]!=';'&&line[0]!=
'|'){int equals=line.IndexOf('=');if(equals>=0&&section.Length>0){VRage.Game.ModAPI.Ingame.Utilities.MyIniKey key;try{key=
new VRage.Game.ModAPI.Ingame.Utilities.MyIniKey(section,line.Substring(0,equals).Trim());}catch(Exception){return Fail(
"Invalid INI key.",out reason);}if(!keys.Add(key))return Fail("Duplicate INI key.",out reason);}}clean.Append(source,start,next-start);}
start=next;}var original=new VRage.Game.ModAPI.Ingame.Utilities.MyIni();var edited=new VRage.Game.ModAPI.Ingame.Utilities.
MyIni();if(!original.TryParse(clean.ToString())||!edited.TryParse(clean.ToString()))return Fail("Invalid INI document.",out
reason);document=new CustomDataDocument(source,original,edited,owned,allowed,tokens,tail);return true;}public bool TryGet(
VRage.Game.ModAPI.Ingame.Utilities.MyIniKey key,out string value){value=null;if(key.IsEmpty||!edited.ContainsKey(key))return
false;value=edited.Get(key).ToString();return true;}public bool HasSetupToken(string token){if(token==null||!allowed.Contains
(token)||consumed.Contains(token))return false;foreach(var line in tokenLines)if(line.Trim()==token)return true;return
false;}public bool TrySeedMissing(VRage.Game.ModAPI.Ingame.Utilities.MyIniKey key,string value,string comment,out string
reason){if(!CanEdit(key,value,out reason))return false;if(edited.ContainsKey(key))return true;edited.Set(key,value);if(comment
!=null)edited.SetComment(key,comment);dirty=true;return true;}public bool TrySetOwned(VRage.Game.ModAPI.Ingame.Utilities.
MyIniKey key,string value,out string reason){if(!CanEdit(key,value,out reason))return false;if(edited.ContainsKey(key)&&edited.
Get(key).ToString()==value)return true;edited.Set(key,value);dirty=true;return true;}public bool TryConsumeSetupToken(
string token,out string reason){reason="";if(ended)return Fail("Document is closed. Reparse current data.",out reason);if(
token==null||!allowed.Contains(token))return Fail("Unknown setup token.",out reason);if(HasSetupToken(token)){consumed.Add(
token);dirty=true;}return true;}public bool TryBuild(out string candidate,out string reason){candidate=null;reason="";if(
ended)return Fail("Document is closed. Reparse current data.",out reason);if(!dirty||(consumed.Count==0&&SameDocument(
original,edited))){candidate=source;return true;}if(!Preserved(original,edited,owned))return Fail(
"Document preservation failed.",out reason);var text=new System.Text.StringBuilder();foreach(var line in tokenLines)if(!consumed.Contains(line.Trim()))
{text.Append(line);if(!line.EndsWith("\n",StringComparison.Ordinal))text.Append('\n');}string iniText=edited.ToString();
text.Append(iniText);if(tail.Length>0){if(text.Length>0&&text[text.Length-1]!='\n')text.Append('\n');text.Append(tail);}var
roundTrip=new VRage.Game.ModAPI.Ingame.Utilities.MyIni();if(!roundTrip.TryParse(iniText)||!Preserved(edited,roundTrip,new HashSet
<VRage.Game.ModAPI.Ingame.Utilities.MyIniKey>()))return Fail("Output preservation failed.",out reason);candidate=text.
ToString();return true;}public CustomDataSaveStatus TrySave(Func<string>read,Action<string>write,out string reason){string
candidate;if(!TryBuild(out candidate,out reason))return CustomDataSaveStatus.Failed;if(candidate==source)return
CustomDataSaveStatus.Unchanged;ended=true;if(read==null||write==null){reason="Null save callback.";return CustomDataSaveStatus.Failed;}
string operation="CustomData read failed before save.";try{if(read()!=source){reason="CustomData changed before save.";return
CustomDataSaveStatus.Conflict;}operation="CustomData write failed.";write(candidate);operation="CustomData readback failed.";if(read()!=
candidate){reason="CustomData readback mismatch.";return CustomDataSaveStatus.Failed;}return CustomDataSaveStatus.Saved;}catch(
Exception){reason=operation;return CustomDataSaveStatus.Failed;}}bool CanEdit(VRage.Game.ModAPI.Ingame.Utilities.MyIniKey key,
string value,out string reason){reason="";if(ended)return Fail("Document is closed. Reparse current data.",out reason);if(key.
IsEmpty||!owned.Contains(key))return Fail("Key is not owned.",out reason);if(value==null)return Fail("Null value.",out reason);
return true;}static bool Preserved(VRage.Game.ModAPI.Ingame.Utilities.MyIni before,VRage.Game.ModAPI.Ingame.Utilities.MyIni
after,HashSet<VRage.Game.ModAPI.Ingame.Utilities.MyIniKey>mutable){var sections=new List<string>();before.GetSections(
sections);foreach(var section in sections)if(!after.ContainsSection(section)||before.GetSectionComment(section)!=after.
GetSectionComment(section))return false;var keys=new List<VRage.Game.ModAPI.Ingame.Utilities.MyIniKey>();before.GetKeys(keys);foreach(var
key in keys)if(!after.ContainsKey(key)||before.GetComment(key)!=after.GetComment(key)||(!mutable.Contains(key)&&before.Get(
key).ToString()!=after.Get(key).ToString()))return false;return(before.EndComment??"").TrimEnd('\r','\n')==(after.
EndComment??"").TrimEnd('\r','\n')&&before.EndContent==after.EndContent;}static bool SameDocument(VRage.Game.ModAPI.Ingame.
Utilities.MyIni before,VRage.Game.ModAPI.Ingame.Utilities.MyIni after){var first=new List<VRage.Game.ModAPI.Ingame.Utilities.
MyIniKey>();var second=new List<VRage.Game.ModAPI.Ingame.Utilities.MyIniKey>();before.GetKeys(first);after.GetKeys(second);var
firstSections=new List<string>();var secondSections=new List<string>();before.GetSections(firstSections);after.GetSections(
secondSections);return first.Count==second.Count&&firstSections.Count==secondSections.Count&&Preserved(before,after,new HashSet<VRage.
Game.ModAPI.Ingame.Utilities.MyIniKey>());}static bool Fail(string message,out string reason){reason=message;return false;}}
public enum BlockSelectionStatus{Selected,NotSelected,Invalid}public static class BlockSelection{public static
BlockSelectionStatus Evaluate(CustomDataDocument document,VRage.Game.ModAPI.Ingame.Utilities.MyIniKey tagKey,string bareTag,out string
reason){reason="";if(document==null||tagKey.IsEmpty||string.IsNullOrWhiteSpace(bareTag)||bareTag!=bareTag.Trim()||bareTag==
"---"||bareTag.IndexOfAny(new[]{'\r','\n','=','[',']',';','|'})>=0){reason="Invalid selection input.";return
BlockSelectionStatus.Invalid;}string value;if(document.TryGet(tagKey,out value)){bool selected;if(!bool.TryParse(value,out selected)){reason
="Invalid tag value: expected true or false.";return BlockSelectionStatus.Invalid;}return selected?BlockSelectionStatus.
Selected:BlockSelectionStatus.NotSelected;}return document.HasSetupToken(bareTag)?BlockSelectionStatus.Selected:
BlockSelectionStatus.NotSelected;}public static bool PlanSetup(CustomDataDocument document,VRage.Game.ModAPI.Ingame.Utilities.MyIniKey
tagKey,string bareTag,out string reason){var status=Evaluate(document,tagKey,bareTag,out reason);if(status!=
BlockSelectionStatus.Selected){if(reason.Length==0)reason="Tag is not selected.";return false;}return document.TrySeedMissing(tagKey,"true",
null,out reason)&&document.TryConsumeSetupToken(bareTag,out reason);}}public enum SpriteDrawingKind{Texture,Text,ClipRect,
ClearClipRect}public struct SpriteDrawingElement{public SpriteDrawingKind Kind;public string Data,FontId;public Vector2?Position,Size
;public Color?Color;public TextAlignment?Alignment;public float?RotationOrScale;}public static class SpriteDrawing{public
static bool TryDrawElements(IMyTerminalBlock block,int surfaceIndex,IList<SpriteDrawingElement>elements,out string reason,
float scale=1f){reason="";if(block==null){reason="Null block.";return false;}if(elements==null){reason=
"Null drawing elements.";return false;}if(!Finite(scale)){reason="Invalid scale.";return false;}IMyTextSurface surface;if(!ResolveSurface(block,
surfaceIndex,out surface,out reason))return false;List<MySprite>sprites;if(!BuildElements(elements,surface.TextureSize*0.5f,surface.
ScriptForegroundColor,scale,out sprites,out reason))return false;Submit(surface,sprites);return true;}static bool BuildElements(IList<
SpriteDrawingElement>elements,Vector2 center,Color color,float scale,out List<MySprite>sprites,out string reason){sprites=new List<MySprite>
();reason="";if(elements==null||!Finite(scale)||!Finite(center)){reason="Invalid drawing arguments.";return false;}for(
int i=0;i<elements.Count;i++){var e=elements[i];var row=new Row{Fields=1,Data=e.Data,Font=e.FontId??"Debug",Color=e.Color??
color,Alignment=e.Alignment??TextAlignment.CENTER,Position=e.Position??Vector2.Zero,Size=e.Size??Vector2.Zero,Rotation=e.
RotationOrScale??0};switch(e.Kind){case SpriteDrawingKind.Texture:row.Type="TEXTURE";break;case SpriteDrawingKind.Text:row.Type="TEXT";
break;case SpriteDrawingKind.ClipRect:row.Type="CLIP_RECT";break;case SpriteDrawingKind.ClearClipRect:row.Type=
"CLEAR_CLIP_RECT";break;default:reason="Element "+(i+1)+": Invalid kind.";return false;}if(e.Data!=null)row.Fields|=2;if(e.Position.
HasValue)row.Fields|=4;if(e.Size.HasValue)row.Fields|=8;if(e.Color.HasValue)row.Fields|=16;if(e.FontId!=null)row.Fields|=32;if(e
.Alignment.HasValue)row.Fields|=64;if(e.RotationOrScale.HasValue)row.Fields|=128;if(!Finite(row.Position)||!Finite(row.
Size)||!Finite(row.Rotation)||row.Font.Length==0||(row.Alignment!=TextAlignment.LEFT&&row.Alignment!=TextAlignment.CENTER&&
row.Alignment!=TextAlignment.RIGHT)){reason="Element "+(i+1)+": Invalid attribute value.";return false;}string error;if(!
Add(row,center,scale,sprites,out error)){reason="Element "+(i+1)+": "+error;return false;}}return true;}static bool
ResolveSurface(IMyTerminalBlock block,int surfaceIndex,out IMyTextSurface surface,out string reason){reason="";surface=null;var
provider=block as IMyTextSurfaceProvider;if(provider!=null){if(surfaceIndex<0||surfaceIndex>=provider.SurfaceCount){reason=
"Invalid surface index.";return false;}surface=provider.GetSurface(surfaceIndex);}else{surface=block as IMyTextSurface;if(surfaceIndex!=0){
reason="Invalid surface index.";return false;}}if(surface==null){reason="No text surface.";return false;}return true;}static
void Submit(IMyTextSurface surface,List<MySprite>sprites){surface.ContentType=ContentType.SCRIPT;surface.Script="";using(var
frame=surface.DrawFrame())foreach(var sprite in sprites)frame.Add(sprite);}struct Row{public int Fields;public string Type,
Data,Font;public Vector2 Position,Size;public Color Color;public TextAlignment Alignment;public float Rotation;}static bool
Add(Row row,Vector2 center,float scale,List<MySprite>sprites,out string error){error="Missing or inapplicable attributes.";
int required,allowed;switch(row.Type){case"TEXTURE":required=11;allowed=223;break;case"TEXT":required=3;allowed=247;break;
case"CLIP_RECT":required=allowed=13;break;case"CLEAR_CLIP_RECT":required=allowed=1;break;default:error=
"Missing or invalid Type.";return false;}if((row.Fields&required)!=required||(row.Fields&~allowed)!=0)return false;if(row.Type=="CLEAR_CLIP_RECT")
{sprites.Add(MySprite.CreateClearClipRect());error="";return true;}if(row.Type=="TEXTURE"&&row.Data.Length==0){error=
"Empty texture name.";return false;}if(row.Type=="TEXT"){if((row.Fields&128)==0)row.Rotation=1;if(row.Rotation<=0){error=
"Invalid text scale.";return false;}row.Rotation*=scale;}if(row.Type=="CLIP_RECT"&&(row.Size.X<0||row.Size.Y<0)){error=
"Negative input clip size.";return false;}bool skipClip=row.Type=="CLIP_RECT"&&(row.Size.X==0||row.Size.Y==0);row.Position=center+row.Position*
scale;row.Size*=scale;if(!Finite(row.Position)||!Finite(row.Size)||!Finite(row.Rotation)){error=
"Non-finite transformed value.";return false;}if(skipClip){error="";return true;}if(row.Type=="CLIP_RECT"){int x,y,w,h;if(!Integer(row.Position.X,out x
)||!Integer(row.Position.Y,out y)||!Integer(row.Size.X,out w)||!Integer(row.Size.Y,out h)||(long)x+w<int.MinValue||(long)
x+w>int.MaxValue||(long)y+h<int.MinValue||(long)y+h>int.MaxValue){error="Clip rectangle out of range.";return false;}
sprites.Add(MySprite.CreateClipRect(new Rectangle(x,y,w,h)));}else{sprites.Add(new MySprite{Type=row.Type=="TEXT"?SpriteType.
TEXT:SpriteType.TEXTURE,Data=row.Data,Position=row.Position,Size=row.Type=="TEXT"?(Vector2?)null:row.Size,Color=row.Color,
FontId=row.Type=="TEXT"?row.Font:null,Alignment=row.Alignment,RotationOrScale=row.Rotation});}error="";return true;}static
bool Integer(float value,out int result){double rounded=Math.Round((double)value,MidpointRounding.AwayFromZero);result=0;if(
rounded<int.MinValue||rounded>int.MaxValue)return false;result=(int)rounded;return true;}static bool Finite(float value){return
!float.IsNaN(value)&&!float.IsInfinity(value);}static bool Finite(Vector2 value){return Finite(value.X)&&Finite(value.Y);
}