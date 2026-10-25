using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;

namespace AcManager.App.Converters;

public sealed class FilePathToBitmapConverter : IValueConverter
{
    public static FilePathToBitmapConverter Instance { get; } = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string path || path.Length == 0 || !File.Exists(path)) {
            return null;
        }

        try {
            return new Bitmap(path);
        } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException or NotSupportedException) {
            return null;
        }
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
