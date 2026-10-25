using System.Text;

namespace AcTools.Platform.Linux;

/// <summary>
/// Reads the Proton tool name Steam stored for one app in config.vdf.
/// Other sections also mention the app id (shader cache, decryption keys) and must not win.
/// </summary>
public static class SteamConfigVdf
{
    public static string? ReadCompatToolName(string vdf, string appId)
    {
        var tokens = Tokenize(vdf);
        var stack = new List<string>();
        string? pendingKey = null;
        foreach (var token in tokens) {
            if (token == "{") {
                stack.Add(pendingKey ?? "");
                pendingKey = null;
                continue;
            }

            if (token == "}") {
                if (stack.Count > 0) {
                    stack.RemoveAt(stack.Count - 1);
                }

                pendingKey = null;
                continue;
            }

            if (pendingKey == null) {
                pendingKey = token;
                continue;
            }

            if (pendingKey == "name" && InCompatMapping(stack, appId)) {
                return token.Length == 0 ? null : token;
            }

            pendingKey = null;
        }

        return null;
    }

    private static bool InCompatMapping(List<string> stack, string appId)
    {
        if (stack.Count < 2 || stack[^1] != appId) {
            return false;
        }

        foreach (var name in stack) {
            if (name == "CompatToolMapping") {
                return true;
            }
        }

        return false;
    }

    private static List<string> Tokenize(string text)
    {
        var tokens = new List<string>();
        for (var i = 0; i < text.Length; i++) {
            var c = text[i];
            if (char.IsWhiteSpace(c)) {
                continue;
            }

            if (c is '{' or '}') {
                tokens.Add(c.ToString());
                continue;
            }

            if (c != '"') {
                continue;
            }

            var value = new StringBuilder();
            for (i++; i < text.Length; i++) {
                if (text[i] == '\\' && i + 1 < text.Length) {
                    value.Append(text[i + 1]);
                    i++;
                    continue;
                }

                if (text[i] == '"') {
                    break;
                }

                value.Append(text[i]);
            }

            tokens.Add(value.ToString());
        }

        return tokens;
    }
}
