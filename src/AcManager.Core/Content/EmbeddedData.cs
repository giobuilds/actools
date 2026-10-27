namespace AcManager.Core.Content;

internal static class EmbeddedData
{
    public static string Read(string fileName)
    {
        var stream = typeof(EmbeddedData).Assembly.GetManifestResourceStream(fileName);
        if (stream == null) {
            return "";
        }

        using (stream) {
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }
    }
}
