using System.Globalization;

namespace DynamicDialog
{
    /// <summary>What a criterion does when the query has no fact for its key.</summary>
    public enum MissingFactPolicy : byte
    {
        /// <summary>The criterion fails. This is the talk's rule and the default.</summary>
        Reject,

        /// <summary>The criterion tests <see cref="Criterion.DefaultValue"/> instead, e.g. "said_X is 0 or unset".</summary>
        UseDefault,
    }

    /// <summary>
    /// A test on one fact: <c>Min &lt;= value &lt;= Max</c>, optionally inverted.
    /// Every comparison (==, !=, &lt;, &lt;=, &gt;, &gt;=, ranges, symbol equality) uses this
    /// same branch-light shape (talk slides 115-118).
    /// </summary>
    public readonly struct Criterion
    {
        /// <summary>The fact being tested.</summary>
        public readonly Symbol Key;

        /// <summary>Lowest value that passes (inclusive).</summary>
        public readonly float Min;

        /// <summary>Highest value that passes (inclusive).</summary>
        public readonly float Max;

        /// <summary>Pass when the value is outside [<see cref="Min"/>, <see cref="Max"/>] instead of inside.</summary>
        public readonly bool Invert;

        /// <summary>What happens when the query has no fact for <see cref="Key"/>.</summary>
        public readonly MissingFactPolicy Missing;

        /// <summary>The value tested in place of a missing fact, when <see cref="Missing"/> is <see cref="MissingFactPolicy.UseDefault"/>.</summary>
        public readonly float DefaultValue;

        /// <summary>The compared value is a symbol (a name). Only affects how the criterion is displayed.</summary>
        public readonly bool ValueIsSymbol;

        /// <summary>A test that passes when <c>min &lt;= value &lt;= max</c> (or the opposite, if <paramref name="invert"/> is set).</summary>
        /// <param name="key">The fact to test.</param>
        /// <param name="min">Lowest passing value (inclusive).</param>
        /// <param name="max">Highest passing value (inclusive).</param>
        /// <param name="invert">Pass when the value is outside the range instead.</param>
        /// <param name="missing">What to do when the fact is missing.</param>
        /// <param name="defaultValue">The value tested for a missing fact, with <see cref="MissingFactPolicy.UseDefault"/>.</param>
        /// <param name="valueIsSymbol">The compared value is a name. Only affects how the criterion is displayed.</param>
        public Criterion(Symbol key, float min, float max, bool invert = false,
            MissingFactPolicy missing = MissingFactPolicy.Reject, float defaultValue = 0f, bool valueIsSymbol = false)
        {
            ValueIsSymbol = valueIsSymbol;
            Key = key;
            Min = min;
            Max = max;
            Invert = invert;
            Missing = missing;
            DefaultValue = defaultValue;
        }

        /// <summary>Tests one fact value against this criterion.</summary>
        /// <param name="present">Whether the query contains the key.</param>
        /// <param name="value">The fact's value, ignored when <paramref name="present"/> is false.</param>
        public bool Test(bool present, float value)
        {
            if (!present)
            {
                if (Missing == MissingFactPolicy.Reject) return false;
                value = DefaultValue;
            }
            bool inside = value >= Min && value <= Max; // NaN is never inside
            return inside != Invert;
        }

        /// <summary>
        /// True when this criterion pins the key to exactly one value and rejects a missing fact,
        /// which makes it usable for partitioning rules into buckets.
        /// </summary>
        /// <param name="value">The pinned value.</param>
        public bool IsExactMatch(out float value)
        {
            value = Min;
            return !Invert && Missing == MissingFactPolicy.Reject && Min == Max;
        }

        /// <summary>Returns a copy that tests <paramref name="defaultValue"/> when the fact is missing.</summary>
        public Criterion WhenMissing(float defaultValue) =>
            new Criterion(Key, Min, Max, Invert, MissingFactPolicy.UseDefault, defaultValue, ValueIsSymbol);

        // ---- Factories -------------------------------------------------------------------

        /// <summary>The fact equals <paramref name="value"/>.</summary>
        public static Criterion Eq(Symbol key, float value) => new Criterion(key, value, value);

        /// <summary>The fact equals the name <paramref name="value"/>.</summary>
        public static Criterion Eq(Symbol key, Symbol value) => new Criterion(key, value.AsValue, value.AsValue, valueIsSymbol: true);

        /// <summary>The fact equals <paramref name="value"/>.</summary>
        public static Criterion Eq(string key, float value) => Eq(Symbol.Of(key), value);

        /// <summary>The fact equals the name <paramref name="symbolValue"/>.</summary>
        public static Criterion Eq(string key, string symbolValue) => Eq(Symbol.Of(key), Symbol.Of(symbolValue));

        /// <summary>The fact does not equal <paramref name="value"/>. A missing fact still fails.</summary>
        public static Criterion NotEq(Symbol key, float value) => new Criterion(key, value, value, invert: true);

        /// <summary>The fact does not equal the name <paramref name="value"/>. A missing fact still fails.</summary>
        public static Criterion NotEq(Symbol key, Symbol value) => new Criterion(key, value.AsValue, value.AsValue, invert: true, valueIsSymbol: true);

        /// <summary>The fact does not equal <paramref name="value"/>. A missing fact still fails.</summary>
        public static Criterion NotEq(string key, float value) => NotEq(Symbol.Of(key), value);

        /// <summary>The fact does not equal the name <paramref name="symbolValue"/>. A missing fact still fails.</summary>
        public static Criterion NotEq(string key, string symbolValue) => NotEq(Symbol.Of(key), Symbol.Of(symbolValue));

        /// <summary>The fact is greater than <paramref name="value"/>.</summary>
        public static Criterion Gt(Symbol key, float value) => new Criterion(key, FloatMath.NextUp(value), float.PositiveInfinity);

        /// <summary>The fact is greater than or equal to <paramref name="value"/>.</summary>
        public static Criterion Ge(Symbol key, float value) => new Criterion(key, value, float.PositiveInfinity);

        /// <summary>The fact is less than <paramref name="value"/>.</summary>
        public static Criterion Lt(Symbol key, float value) => new Criterion(key, float.NegativeInfinity, FloatMath.NextDown(value));

        /// <summary>The fact is less than or equal to <paramref name="value"/>.</summary>
        public static Criterion Le(Symbol key, float value) => new Criterion(key, float.NegativeInfinity, value);

        /// <summary>The fact is greater than <paramref name="value"/>.</summary>
        public static Criterion Gt(string key, float value) => Gt(Symbol.Of(key), value);

        /// <summary>The fact is greater than or equal to <paramref name="value"/>.</summary>
        public static Criterion Ge(string key, float value) => Ge(Symbol.Of(key), value);

        /// <summary>The fact is less than <paramref name="value"/>.</summary>
        public static Criterion Lt(string key, float value) => Lt(Symbol.Of(key), value);

        /// <summary>The fact is less than or equal to <paramref name="value"/>.</summary>
        public static Criterion Le(string key, float value) => Le(Symbol.Of(key), value);

        /// <summary>The fact is in the inclusive range [<paramref name="min"/>, <paramref name="max"/>].</summary>
        public static Criterion Between(Symbol key, float min, float max) => new Criterion(key, min, max);

        /// <summary>The fact is in the inclusive range [<paramref name="min"/>, <paramref name="max"/>].</summary>
        public static Criterion Between(string key, float min, float max) => Between(Symbol.Of(key), min, max);

        /// <summary>The fact exists, whatever its value.</summary>
        public static Criterion Exists(Symbol key) => new Criterion(key, float.NegativeInfinity, float.PositiveInfinity);

        /// <summary>The fact exists, whatever its value.</summary>
        public static Criterion Exists(string key) => Exists(Symbol.Of(key));

        /// <summary>The fact is 0 or missing: the usual "this hasn't happened yet" test.</summary>
        public static Criterion ZeroOrMissing(Symbol key) => Eq(key, 0f).WhenMissing(0f);

        /// <summary>The fact is 0 or missing: the usual "this hasn't happened yet" test.</summary>
        public static Criterion ZeroOrMissing(string key) => ZeroOrMissing(Symbol.Of(key));

        /// <summary>The criterion in readable form, e.g. "health &lt;= 30" or "map == swamp".</summary>
        public override string ToString()
        {
            string k = Key.ToString();
            string body;
            if (Min == Max)
            {
                string v = FactValue.Format(Key, Min, ValueIsSymbol);
                body = Invert ? $"{k} != {v}" : $"{k} == {v}";
            }
            else if (float.IsNegativeInfinity(Min) && float.IsPositiveInfinity(Max))
                body = Invert ? $"{k} <never>" : $"{k} exists";
            else if (float.IsPositiveInfinity(Max))
                body = $"{k} >= {F(Min)}";
            else if (float.IsNegativeInfinity(Min))
                body = $"{k} <= {F(Max)}";
            else
                body = $"{k} in [{F(Min)}, {F(Max)}]";
            if (Invert && Min != Max) body = "not (" + body + ")";
            if (Missing == MissingFactPolicy.UseDefault) body += $" (missing = {F(DefaultValue)})";
            return body;
        }

        static string F(float v) => v.ToString("G9", CultureInfo.InvariantCulture);
    }
}
