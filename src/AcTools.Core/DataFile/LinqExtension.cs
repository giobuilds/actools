#nullable disable
using System.Globalization;

namespace AcTools.Utils.Helpers {
    static class LinqExtension {
        public static IEnumerable<int> RangeFrom(int from = 0) {
            for (var i = from; i < int.MaxValue; i++) {
                yield return i;
            }
        }

        public static void ForEach<TFirst, TSecond>(this IEnumerable<TFirst> source, IEnumerable<TSecond> second, Action<TFirst, TSecond> action) {
            using (var first = source.GetEnumerator())
            using (var other = second.GetEnumerator()) {
                while (first.MoveNext() && other.MoveNext()) {
                    action(first.Current, other.Current);
                }
            }
        }

        public static string JoinToString<T>(this IEnumerable<T> enumerable, char separator) {
            return string.Join(separator.ToString(), enumerable);
        }

        public static string ToInvariantString(this object value) {
            if (value == null) throw new ArgumentNullException(nameof(value));
            switch (value) {
                case string text:
                    return text;
                case double number:
                    return number.ToString(CultureInfo.InvariantCulture);
                case float number:
                    return number.ToString(CultureInfo.InvariantCulture);
                case int number:
                    return number.ToString(CultureInfo.InvariantCulture);
                case long number:
                    return number.ToString(CultureInfo.InvariantCulture);
                default:
                    return value.ToString();
            }
        }
    }
}
