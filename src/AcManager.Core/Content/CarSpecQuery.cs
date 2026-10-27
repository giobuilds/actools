using System.Globalization;
using System.Text.RegularExpressions;
using StringBasedFilter;
using StringBasedFilter.TestEntries;

namespace AcManager.Core.Content;

/// <summary>
/// Unit suffixes for car spec search. Power is compared in bhp, torque in Nm, weight in kilograms,
/// and top speed in kilometres per hour. A query with no suffix uses those units.
/// Acceleration follows Content Manager's second-based time entry: a whole-number query rounds the
/// stored seconds, and an equal match allows almost one second. A comma in <c>1,250</c> is a thousands
/// separator, and the same comma in <c>1,250 kg</c> is read as a decimal point.
/// </summary>
internal static class CarSpecQuery
{
    private static readonly Regex LooseNumber = new(
        @"-? *\d+([\.,]\d*)?",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex LooseInt = new(
        @"-? *\d+",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static readonly ITestEntryFactory Power = new MultiplierFactory(PowerMultiplier);

    public static readonly ITestEntryFactory Torque = new MultiplierFactory(TorqueMultiplier);

    public static readonly ITestEntryFactory Weight = new MultiplierFactory(WeightMultiplier);

    public static readonly ITestEntryFactory Speed = new MultiplierFactory(SpeedMultiplier);

    public static readonly ITestEntryFactory Seconds = new SecondsFactory();

    public static readonly ITestEntryFactory Units = new MultiplierFactory(UnitsMultiplier);

    internal static bool TryParse(string? text, out double value) => TryNumber(text, out value);

    private static bool TryNumber(string? text, out double value)
    {
        if (text != null && double.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out value)) {
            return true;
        }

        var match = text == null ? Match.Empty : LooseNumber.Match(text);
        if (!match.Success) {
            value = 0d;
            return false;
        }

        return double.TryParse(
            match.Value.Replace(',', '.').Replace(" ", ""),
            NumberStyles.Any,
            CultureInfo.InvariantCulture,
            out value);
    }

    private static bool TryInt(string? text, out int value)
    {
        if (text == null) {
            value = 0;
            return false;
        }

        if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                && int.TryParse(text.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value)
            || int.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out value)) {
            return true;
        }

        var match = LooseInt.Match(text);
        if (match.Success) {
            return int.TryParse(match.Value.Replace(" ", ""), NumberStyles.Any, CultureInfo.InvariantCulture, out value);
        }

        value = 0;
        return false;
    }

    private static string? Postfix(string? value, int maxLength)
    {
        if (string.IsNullOrEmpty(value)) {
            return null;
        }

        for (var i = 0; i < value.Length; i++) {
            if (!char.IsLetter(value[i]) && value[i] != '/') {
                continue;
            }

            var end = i + 1;
            while (end < value.Length && end - i < maxLength && (char.IsLetter(value[end]) || value[end] == '/')) {
                end++;
            }

            return value[i..end].ToLowerInvariant().Replace("/", "");
        }

        return null;
    }

    private static double PowerMultiplier(string? postfix)
    {
        switch (postfix) {
            case "gw":
                return 1341020d;
            case "mw":
                return 1341.02;
            case "kw":
                return 1.34102;
            case "w":
                return 0.00134102;
            default:
                return 1d;
        }
    }

    private static double TorqueMultiplier(string? postfix)
    {
        switch (postfix) {
            case "ft":
            case "fl":
            case "ftlb":
                return 1.35581795;
            default:
                return 1d;
        }
    }

    private static double WeightMultiplier(string? postfix)
    {
        switch (postfix) {
            case "t":
            case "tonn":
                return 1000d;
            case "st":
            case "ston":
                return 6.35029;
            case "lb":
            case "lbs":
                return 0.453592;
            case "oz":
            case "ounc":
                return 0.0283495;
            case "g":
            case "gram":
                return 0.001;
            default:
                return 1d;
        }
    }

    private static double UnitsMultiplier(string? postfix)
    {
        switch (postfix) {
            case "kkk":
                return 1e9;
            case "kk":
                return 1e6;
            case "k":
            case "thou":
                return 1e3;
            case "hund":
                return 1e2;
            default:
                return 1d;
        }
    }

    private static double SpeedMultiplier(string? postfix)
    {
        switch (postfix) {
            case "ms":
            case "mps":
                return 3.6;
            case "knot":
                return 1.852;
            case "kph":
            case "kmh":
                return 1d;
            case "mph":
            case "mh":
                return 1.60934;
            case "fps":
            case "fs":
                return 1.09728;
            default:
                return 1d;
        }
    }

    private static double? NumberOrNull(string? text) => TryNumber(text, out var value) ? value : null;

    /// <summary>
    /// Content Manager's <c>TimeSpanTestEntry</c> with the default postfix <c>second</c>.
    /// A recognised postfix, including that default, is never an exact comparison.
    /// A colon value such as <c>0:05.5</c> is exact when it contains <c>.</c> or <c>,</c>.
    /// </summary>
    private static bool TrySeconds(string? value, out double seconds, out bool exact)
    {
        if (value == null) {
            seconds = 0d;
            exact = false;
            return false;
        }

        var postfix = Postfix(value, 2) ?? (value.IndexOf(':') == -1 ? "se" : null);
        var fromPostfix = SecondsFromPostfix(NumberOrNull(value) ?? 1d, postfix);
        if (fromPostfix.HasValue) {
            seconds = fromPostfix.Value;
            exact = false;
            return true;
        }

        var parts = value.Split(':');
        double? result;
        switch (parts.Length) {
            case 2:
                result = NumberOrNull(parts[0]) * 60d + NumberOrNull(parts[1]);
                break;
            case 3:
                result = (NumberOrNull(parts[0]) * 60d + NumberOrNull(parts[1])) * 60d + NumberOrNull(parts[2]);
                break;
            case 4:
                result = ((NumberOrNull(parts[0]) * 24d + NumberOrNull(parts[1])) * 60d + NumberOrNull(parts[2])) * 60d
                    + NumberOrNull(parts[3]);
                break;
            default:
                seconds = 0d;
                exact = false;
                return false;
        }

        if (!result.HasValue) {
            seconds = 0d;
            exact = false;
            return false;
        }

        seconds = TimeSpan.FromSeconds(result.Value).TotalSeconds;
        exact = value.IndexOf('.') != -1 || value.IndexOf(',') != -1;
        return true;
    }

    private static double? SecondsFromPostfix(double value, string? postfix)
    {
        switch (postfix) {
            case "y":
            case "ye":
            case "yr":
                return TimeSpan.FromDays(value * 365d).TotalSeconds;
            case "mo":
            case "mn":
                return TimeSpan.FromDays(value * 30d).TotalSeconds;
            case "w":
            case "we":
            case "wk":
                return TimeSpan.FromDays(value * 7d).TotalSeconds;
            case "d":
            case "da":
                return TimeSpan.FromDays(value).TotalSeconds;
            case "h":
            case "hr":
            case "ho":
                return TimeSpan.FromHours(value).TotalSeconds;
            case "m":
            case "mi":
                return TimeSpan.FromMinutes(value).TotalSeconds;
            case "s":
            case "sc":
            case "se":
                return TimeSpan.FromSeconds(value).TotalSeconds;
            case "ms":
                return TimeSpan.FromMilliseconds(value).TotalSeconds;
            default:
                return null;
        }
    }

    private sealed class MultiplierFactory : ITestEntryFactory
    {
        private readonly Func<string?, double> _multiplier;

        public MultiplierFactory(Func<string?, double> multiplier)
        {
            _multiplier = multiplier;
        }

        public ITestEntry Create(Operator op, string value)
        {
            if (!TryNumber(value, out var number)) {
                return null!;
            }

            return new ScaledComparison(op, number * _multiplier(Postfix(value, 4)), _multiplier);
        }
    }

    private sealed class ScaledComparison : ITestEntry
    {
        private readonly Operator _op;
        private readonly double _expected;
        private readonly Func<string?, double> _multiplier;

        public ScaledComparison(Operator op, double expected, Func<string?, double> multiplier)
        {
            _op = op;
            _expected = expected;
            _multiplier = multiplier;
        }

        public void Set(ITestEntryFactory factory)
        {
        }

        public bool Test(string value)
        {
            if (!TryNumber(value, out var number)) {
                return false;
            }

            return Test(number * _multiplier(Postfix(value, 4)));
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

        public bool Test(DateTime value) => Test(value.TimeOfDay);
    }

    private sealed class SecondsFactory : ITestEntryFactory
    {
        public ITestEntry Create(Operator op, string value)
        {
            if (!TrySeconds(value, out var seconds, out var exact)) {
                return null!;
            }

            return new SecondsComparison(op, seconds, exact);
        }
    }

    private sealed class SecondsComparison : ITestEntry
    {
        private readonly Operator _op;
        private readonly double _seconds;
        private readonly bool _exact;

        public SecondsComparison(Operator op, double seconds, bool exact)
        {
            _op = op;
            _seconds = seconds;
            _exact = exact;
        }

        public void Set(ITestEntryFactory factory)
        {
        }

        public bool Test(string value)
        {
            if (value == null) {
                return false;
            }

            if (TrySeconds(value, out var seconds, out _)) {
                return Test(seconds);
            }

            return TryInt(value, out var whole) && Test(whole);
        }

        public bool Test(double value)
        {
            var compared = _exact ? value : Math.Round(value);
            switch (_op) {
                case Operator.Less:
                    return compared < _seconds;
                case Operator.LessEqual:
                    return compared <= _seconds;
                case Operator.More:
                    return compared > _seconds;
                case Operator.MoreEqual:
                    return compared >= _seconds;
                case Operator.Equal:
                    return Math.Abs(compared - _seconds) < 1d;
                default:
                    return false;
            }
        }

        public bool Test(bool value) => Test(value ? 1d : 0d);

        public bool Test(TimeSpan value) => Test(value.TotalSeconds);

        public bool Test(DateTime value) => Test(value.TimeOfDay);
    }
}
