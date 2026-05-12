namespace DendroKit.Core.Params;

/// <summary>
/// Seeded random generator with save/restore state, matching Java arbaro Random.
/// Uses a linear congruential generator identical to java.util.Random.
/// </summary>
public sealed class TreeRandom
{
    // Java LCG constants
    private const long Multiplier = 0x5DEECE66DL;
    private const long Addend     = 0xBL;
    private const long Mask       = (1L << 48) - 1;

    private long _seed;

    public TreeRandom(long seed) => SetSeed(seed);

    public void SetSeed(long seed) => _seed = (seed ^ Multiplier) & Mask;

    public long GetState()
    {
        // Match Arbaro's Java Random.getState():
        // it advances via nextLong(), then reseeds from that value.
        long state = NextLong();
        SetSeed(state);
        return state;
    }

    public void SetState(long state) => SetSeed(state);

    private int Next(int bits)
    {
        _seed = (_seed * Multiplier + Addend) & Mask;
        return (int)(_seed >> (48 - bits));
    }

    public double NextDouble() => ((long)Next(26) * (1L << 27) + Next(27)) / (double)(1L << 53);

    public long NextLong()
    {
        long hi = (long)Next(32) << 32;
        long lo = Next(32) & 0xFFFFFFFFL;
        return hi | lo;
    }

    /// <summary>Returns a uniform value in [low, high).</summary>
    public double Uniform(double low, double high) =>
        low + (high - low) * NextDouble();
}
