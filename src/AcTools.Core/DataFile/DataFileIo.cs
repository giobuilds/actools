#nullable disable
using System.Globalization;

namespace AcTools.DataFile {
    /// <summary>
    /// The Windows writer sends the previous file to the recycle bin.
    /// Linux keeps that copy under a numbered name instead.
    /// </summary>
    static class DataFileIo {
        public static string Unique(string filename, string postfix = "-{0}") {
            if (!File.Exists(filename)) {
                return filename;
            }

            var ext = Path.GetExtension(filename);
            var start = filename.Substring(0, filename.Length - ext.Length);
            for (var i = 1; i < 99999; i++) {
                var result = start + string.Format(CultureInfo.InvariantCulture, postfix, i) + ext;
                if (!File.Exists(result)) {
                    return result;
                }
            }

            throw new IOException("Can’t find unique filename");
        }

        public static bool TryDelete(string filename) {
            try {
                if (File.Exists(filename)) {
                    File.Delete(filename);
                }

                return true;
            } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) {
                return false;
            }
        }

        public sealed class OriginalHolder : IDisposable {
            private readonly string _filename;

            public OriginalHolder(string filename) {
                _filename = filename;
                Filename = Unique(filename, ".tmp-{0}");
            }

            public string Filename { get; }

            public void Dispose() {
                if (!File.Exists(Filename) || string.Equals(Filename, _filename, StringComparison.Ordinal)) {
                    return;
                }

                if (File.Exists(_filename)) {
                    File.Copy(_filename, Unique(_filename));
                    File.Move(Filename, _filename, overwrite: true);
                    return;
                }

                File.Move(Filename, _filename);
            }
        }
    }
}
