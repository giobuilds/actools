#nullable disable
using System.Linq;

namespace AcTools.Utils.Helpers {
    public interface IWithId<out T> {
        T Id { get; }
    }

    public interface IWithId : IWithId<string> { }

    static class StringExtension {
        public static string Or(this string a, string b, params string[] c) {
            return !string.IsNullOrWhiteSpace(a) ? a : !string.IsNullOrWhiteSpace(b) ? b
                    : c.FirstOrDefault(s => !string.IsNullOrWhiteSpace(s));
        }
    }
}
