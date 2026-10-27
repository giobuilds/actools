using System.Globalization;
using System.Text.RegularExpressions;
using StringBasedFilter;
using StringBasedFilter.TestEntries;

namespace AcManager.Core.Content;

/// <summary>
/// Length and width comparisons for track search.
/// The stored length becomes metres the way <c>TrackObjectBase.GetSpecsLengthValue</c> does, then kilometres.
/// A query can say <c>km</c>, <c>m</c>, or <c>mi</c>. The default length unit is kilometres and the default width unit is metres.
/// </summary>
internal static class TrackDistanceQuery
{
    private static readonly Regex LengthFix = new(
        @"^\W*(\d+(?:[\.,]\d+)?)\W*(?:(km|kilometers)|(mi|miles?)|m)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex DotFix = new(
        @"\d[\.,]\d{3}(?=\D|$)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex LooseNumber = new(
        @"-? *\d+([\.,]\d*)?",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static readonly ITestEntryFactory Kilometers = new KilometersFactory();

    public static readonly ITestEntryFactory Meters = new MetersFactory();

    public static double LengthKilometers(string? text) => (LengthMeters(text) ?? 0d) / 1000d;

    private static double? LengthMeters(string? original)
    {
        if (original == null) {
            return null;
        }

        double value;
        if (!double.TryParse(original, NumberStyles.Float | NumberStyles.Integer, CultureInfo.InvariantCulture, out value)) {
            var match = LengthFix.Match(original);
            if (match.Success) {
                var number = match.Groups[1].Value.Replace(',', '.');
                if (!double.TryParse(number, NumberStyles.Float, CultureInfo.InvariantCulture, out value)) {
                    return null;
                }

                if (match.Groups[2].Success) {
                    value *= 1e3;
                } else if (match.Groups[3].Success) {
                    // GetSpecsLengthValue uses 1.6 km per mile for a length written in the JSON.
                    value *= 1.6e3;
                }
            } else if (!TryNumber(original, out value)) {
                return null;
            }
        }

        // A value under 100 with three digits after the separator is a thousands separator.
        if (value < 100 && DotFix.IsMatch(original)) {
            value *= 1e3;
        }

        return value;
    }

    private static bool TryNumber(string? text, out double value)
    {
        if (text != null && double.TryParse(text, NumberStyles.Float | NumberStyles.Integer, CultureInfo.InvariantCulture, out value)) {
            return true;
        }

        var match = text == null ? Match.Empty : LooseNumber.Match(text);
        if (!match.Success) {
            value = 0d;
            return false;
        }

        return double.TryParse(
            match.Value.Replace(',', '.').Replace(" ", ""),
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out value);
    }

    private static string? Postfix(string? value)
    {
        if (string.IsNullOrEmpty(value)) {
            return null;
        }

        for (var i = 0; i < value.Length; i++) {
            if (!char.IsLetter(value[i]) && value[i] != '/') {
                continue;
            }

            var end = i + 1;
            while (end < value.Length && end - i < 4 && (char.IsLetter(value[end]) || value[end] == '/')) {
                end++;
            }

            return value[i..end].ToLowerInvariant().Replace("/", "");
        }

        return null;
    }

    private static double KilometerMultiplier(string? postfix)
    {
        switch (postfix) {
            case "mi":
            case "mile":
                return 1.60934;
            case "k":
            case "km":
            case "kilo":
                return 1d;
            case "m":
            case "metr":
            case "mete":
                return 0.001;
            case "yd":
            case "yard":
                return 0.0009144;
            case "ft":
            case "foot":
            case "feet":
                return 0.0003048;
            case "in":
            case "inch":
                return 2.54E-05;
            case "cm":
            case "cent":
                return 1E-05;
            case "mm":
            case "mill":
                return 1E-06;
            default:
                return 1d;
        }
    }

    private static double MeterMultiplier(string? postfix)
    {
        switch (postfix) {
            case "mi":
            case "mile":
                return 1609.34;
            case "k":
            case "km":
            case "kilo":
                return 1e3;
            case "m":
            case "meter":
                return 1d;
            case "yd":
            case "yard":
                return 0.9144;
            case "ft":
            case "foot":
            case "feet":
                return 0.3048;
            case "in":
            case "inch":
                return 0.0254;
            case "cm":
            case "cent":
                return 0.01;
            case "mm":
            case "mill":
                return 0.001;
            default:
                return 1d;
        }
    }

    private sealed class KilometersFactory : ITestEntryFactory
    {
        public ITestEntry Create(Operator op, string value)
        {
            if (!TryNumber(value, out var number)) {
                return null!;
            }

            return new DistanceComparison(op, number * KilometerMultiplier(Postfix(value)), meters: false);
        }
    }

    private sealed class MetersFactory : ITestEntryFactory
    {
        public ITestEntry Create(Operator op, string value)
        {
            if (!TryNumber(value, out var number)) {
                return null!;
            }

            return new DistanceComparison(op, number * MeterMultiplier(Postfix(value)), meters: true);
        }
    }

    private sealed class DistanceComparison : ITestEntry
    {
        private readonly Operator _op;
        private readonly double _expected;
        private readonly bool _meters;

        public DistanceComparison(Operator op, double expected, bool meters)
        {
            _op = op;
            _expected = expected;
            _meters = meters;
        }

        public void Set(ITestEntryFactory factory)
        {
        }

        public bool Test(string value)
        {
            if (!TryNumber(value, out var number)) {
                return false;
            }

            var multiplier = _meters ? MeterMultiplier(Postfix(value)) : KilometerMultiplier(Postfix(value));
            return Test(number * multiplier);
        }

        public bool Test(double value)
        {
            switch (_op) {
                case Operator.Less:
                    return value < _expected;
                case Operator.LessEqual:
                    return value <= _expected;
                case Operator.More:
                    return value > _expected;
                case Operator.MoreEqual:
                    return value >= _expected;
                case Operator.Equal:
                    return Math.Abs(value - _expected) < 0.0001;
                default:
                    return false;
            }
        }

        public bool Test(bool value) => Test(value ? 1d : 0d);

        public bool Test(TimeSpan value) => Test(value.TotalSeconds);

        public bool Test(DateTime value) => Test(value.TimeOfDay.TotalSeconds);
    }
}
