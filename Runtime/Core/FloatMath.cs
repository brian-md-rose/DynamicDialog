using System;

namespace DynamicDialog
{
    /// <summary>
    /// Neighbouring-float helpers. They turn strict comparisons into inclusive ones,
    /// so every criterion becomes a closed interval (talk slide 119).
    /// </summary>
    internal static class FloatMath
    {
        /// <summary>Smallest float greater than <paramref name="x"/>.</summary>
        public static float NextUp(float x)
        {
            if (float.IsNaN(x) || float.IsPositiveInfinity(x)) return x;
            if (x == 0f) return float.Epsilon; // covers -0 as well
            int bits = BitConverter.SingleToInt32Bits(x);
            bits += x > 0f ? 1 : -1;
            return BitConverter.Int32BitsToSingle(bits);
        }

        /// <summary>Largest float less than <paramref name="x"/>.</summary>
        public static float NextDown(float x) => -NextUp(-x);
    }
}
