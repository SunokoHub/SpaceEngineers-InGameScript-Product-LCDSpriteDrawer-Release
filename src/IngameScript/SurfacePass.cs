namespace IngameScript
{
    // Product-local continuation state. No surface or frame survives a yield.
    public sealed class SurfacePass
    {
        // Conservative admission values retained after real-game calibration.
        public const int MaxElements = 32;
        const int BaseReserve = 10000, ElementReserve = 600;
        public int Index { get; private set; }
        public bool Pending { get; private set; }
        public double Time { get; private set; }
        bool sampled;

        public void Begin() { Index = 0; Pending = true; sampled = false; Time = 0; }
        public double Sample(double elapsed)
        { if (!sampled) { Time = elapsed; sampled = true; } return Time; }
        public bool TryContinue(bool periodic)
        {
            if (!Pending && periodic) Begin();
            return Pending;
        }

        public void Advance() { Index++; }
        public void Cancel() { Index = 0; Pending = false; sampled = false; Time = 0; }

        public static int RequiredInstructions(int elements) => BaseReserve + ElementReserve * elements;

        public static bool CanStart(int used, int maximum, int elements)
        {
            if (used < 0 || elements < 0 || elements > MaxElements) return false;
            int reserve = RequiredInstructions(elements);
            return used <= maximum - reserve;
        }
    }
}
