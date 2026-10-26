namespace AcManager.Core.Content;

/// <summary>
/// String order copied from <c>AlphanumComparatorFast</c>. Skin lists use it for folder ids.
/// Digit runs compare as numbers, and leading zeros do not add to the number's length.
/// </summary>
public sealed class AlphanumOrder : IComparer<string>
{
    public static readonly AlphanumOrder Instance = new();

    public int Compare(string? x, string? y) => Compare(x, y, ignoreCase: true);

    public static int Compare(string? x, string? y, bool ignoreCase = true)
    {
        if (x == null) {
            return y == null ? 0 : 1;
        }

        if (y == null) {
            return -1;
        }

        var len1 = x.Length;
        var len2 = y.Length;
        var marker1 = 0;
        var marker2 = 0;
        var space1 = new char[len1];
        var space2 = new char[len2];

        while (marker1 < len1 && marker2 < len2) {
            var ch1 = x[marker1];
            var ch2 = y[marker2];
            var loc1 = 0;
            var num1 = IsDigit(ch1);
            do {
                space1[loc1] = ch1;
                if (loc1 > 0 || ch1 != '0') {
                    ++loc1;
                }

                marker1++;
                if (marker1 < len1) {
                    ch1 = x[marker1];
                } else {
                    break;
                }
            } while (IsDigit(ch1) == num1);

            var loc2 = 0;
            var num2 = IsDigit(ch2);
            do {
                space2[loc2] = ch2;
                if (loc2 > 0 || ch2 != '0') {
                    ++loc2;
                }

                marker2++;
                if (marker2 < len2) {
                    ch2 = y[marker2];
                } else {
                    break;
                }
            } while (IsDigit(ch2) == num2);

            var result = num1 && num2
                ? CompareNumericBit(space1, loc1, space2, loc2)
                : CompareBit(space1, loc1, space2, loc2, ignoreCase);
            if (result != 0) {
                return result;
            }
        }

        return len1 - len2;
    }

    private static bool IsDigit(char c) => c >= '0' && c <= '9';

    private static int CompareBit(char[] a, int al, char[] b, int bl, bool ignoreCase)
    {
        var length = Math.Min(al, bl);
        for (var i = 0; i < length; ++i) {
            var ac = a[i];
            var bc = b[i];
            if (ignoreCase) {
                ac = char.ToLowerInvariant(ac);
                bc = char.ToLowerInvariant(bc);
            }

            var difference = ac - bc;
            if (difference != 0) {
                return difference;
            }
        }

        return al - bl;
    }

    private static int CompareNumericBit(char[] a, int al, char[] b, int bl)
    {
        if (al != bl) {
            return al - bl;
        }

        for (var i = 0; i < al; ++i) {
            var difference = a[i] - b[i];
            if (difference != 0) {
                return difference;
            }
        }

        return 0;
    }
}
