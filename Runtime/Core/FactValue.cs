using System;
using System.Globalization;

namespace DynamicDialog
{
    /// <summary>The comparison an authored criterion uses. Shared by inspector data and the importers.</summary>
    public enum CriterionOp : byte
    {
        /// <summary>value == Value.</summary>
        Equals,

        /// <summary>value != Value.</summary>
        NotEquals,

        /// <summary>value &lt; Value.</summary>
        Less,

        /// <summary>value &lt;= Value.</summary>
        LessOrEqual,

        /// <summary>value &gt; Value.</summary>
        Greater,

        /// <summary>value &gt;= Value.</summary>
        GreaterOrEqual,

        /// <summary>Inclusive range [value, value2].</summary>
        Between,

        /// <summary>The fact is present, whatever its value.</summary>
        Exists,

        /// <summary>The fact is 0 or missing.</summary>
        ZeroOrMissing,
    }

    /// <summary>Helpers that turn authored text into fact values and criteria.</summary>
    public static class FactValue
    {
        /// <summary>
        /// Parses authored text: numbers use invariant culture, "true"/"false" become 1/0,
        /// and anything else is interned as a symbol. Empty text is 0.
        /// </summary>
        public static float Parse(string text) => Parse(text, out _);

        /// <summary>Like <see cref="Parse(string)"/>, and also says whether the text was a name.</summary>
        /// <param name="text">The authored value.</param>
        /// <param name="isSymbol">True when the text was a name rather than a number or true/false.</param>
        public static float Parse(string text, out bool isSymbol)
        {
            isSymbol = false;
            if (string.IsNullOrWhiteSpace(text)) return 0f;
            text = text.Trim();
            if (float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float f)) return f;
            if (string.Equals(text, "true", StringComparison.OrdinalIgnoreCase)) return 1f;
            if (string.Equals(text, "false", StringComparison.OrdinalIgnoreCase)) return 0f;
            isSymbol = true;
            return Symbol.Of(text).AsValue;
        }

        /// <summary>Keys whose values are always names, so tools show them by name.</summary>
        static readonly Symbol[] SymbolKeys =
        {
            WellKnownFacts.Concept, WellKnownFacts.Who, WellKnownFacts.From, WellKnownFacts.Map, WellKnownFacts.Object, WellKnownFacts.Area,
        };

        internal static bool IsSymbolKey(Symbol key) => Array.IndexOf(SymbolKeys, key) >= 0;

        /// <summary>A fact value as text: the symbol's name if it is one (or the key always holds names), otherwise the number.</summary>
        /// <param name="key">The fact's key. Some well-known keys, like "who", always hold names.</param>
        /// <param name="value">The fact's value.</param>
        /// <param name="isSymbol">Whether the value is known to be a symbol.</param>
        public static string Format(Symbol key, float value, bool isSymbol)
        {
            if ((isSymbol || IsSymbolKey(key)) && SymbolTable.TryFromValue(value, out Symbol s)) return s.Name;
            return value.ToString("G9", CultureInfo.InvariantCulture);
        }

        /// <summary>Builds a criterion from an authored comparison.</summary>
        /// <param name="key">The fact to test.</param>
        /// <param name="op">The comparison.</param>
        /// <param name="value">The value to compare with (the lower bound for <see cref="CriterionOp.Between"/>).</param>
        /// <param name="value2">The upper bound, used only by <see cref="CriterionOp.Between"/>.</param>
        /// <param name="valueIsSymbol">The value is a name. Only affects how the criterion is displayed.</param>
        public static Criterion CreateCriterion(Symbol key, CriterionOp op, float value, float value2 = 0f, bool valueIsSymbol = false)
        {
            switch (op)
            {
                case CriterionOp.Equals: return new Criterion(key, value, value, valueIsSymbol: valueIsSymbol);
                case CriterionOp.NotEquals: return new Criterion(key, value, value, invert: true, valueIsSymbol: valueIsSymbol);
                case CriterionOp.Less: return Criterion.Lt(key, value);
                case CriterionOp.LessOrEqual: return Criterion.Le(key, value);
                case CriterionOp.Greater: return Criterion.Gt(key, value);
                case CriterionOp.GreaterOrEqual: return Criterion.Ge(key, value);
                case CriterionOp.Between: return Criterion.Between(key, Math.Min(value, value2), Math.Max(value, value2));
                case CriterionOp.Exists: return Criterion.Exists(key);
                case CriterionOp.ZeroOrMissing: return Criterion.ZeroOrMissing(key);
                default: throw new ArgumentOutOfRangeException(nameof(op), op, null);
            }
        }
    }
}
