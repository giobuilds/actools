using System.Text;

namespace AcManager.Core.Content;

/// <summary>
/// Assetto Corsa exits when a KN5 material names a shader it does not have.
/// These names come from Custom Shaders Patch. The replacements are shaders used by the original cars and tracks.
/// </summary>
public static class Kn5ShaderFallback
{
    private static readonly Dictionary<string, string> Replacements = new(StringComparer.Ordinal)
    {
        ["ksPerPixelAT_NM_emissive"] = "ksPerPixelAT_NM",
        ["ksPerPixelNM_UVMult"] = "ksPerPixelNM",
    };

    public static int RewriteTree(string directory)
    {
        if (!Directory.Exists(directory)) {
            return 0;
        }

        var changed = 0;
        foreach (var file in Directory.EnumerateFiles(directory, "*.kn5", SearchOption.AllDirectories)) {
            try {
                changed += Rewrite(file);
            } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) {
            }
        }

        return changed;
    }

    public static int Rewrite(string kn5Path)
    {
        var data = File.ReadAllBytes(kn5Path);
        if (!TryPlan(data, out var edits) || edits.Count == 0) {
            return 0;
        }

        var rewritten = Apply(data, edits);
        var backup = kn5Path + ".bak";
        if (!File.Exists(backup)) {
            File.Copy(kn5Path, backup);
        }

        var temporary = kn5Path + ".tmp";
        File.WriteAllBytes(temporary, rewritten);
        File.Move(temporary, kn5Path, overwrite: true);
        return edits.Count;
    }

    private static bool TryPlan(byte[] data, out List<Edit> edits)
    {
        edits = [];
        if (data.Length < 10 || data[0] != (byte)'s' || data[1] != (byte)'c' || Encoding.ASCII.GetString(data, 0, 6) != "sc6969") {
            return false;
        }

        var offset = 6;
        if (!TryReadInt(data, ref offset, out var version)) {
            return false;
        }

        if (version > 5) {
            offset += 4;
        }

        if (!TryReadInt(data, ref offset, out var textures) || textures < 0) {
            return false;
        }

        for (var i = 0; i < textures; i++) {
            offset += 4;
            if (!TrySkipString(data, ref offset) || !TryReadInt(data, ref offset, out var length) || length < 0) {
                return false;
            }

            offset += length;
        }

        if (!TryReadInt(data, ref offset, out var materials) || materials < 0) {
            return false;
        }

        for (var i = 0; i < materials; i++) {
            if (!TrySkipString(data, ref offset)) {
                return false;
            }

            var shaderAt = offset;
            if (!TryReadString(data, ref offset, out var shader)) {
                return false;
            }

            if (Replacements.TryGetValue(shader, out var replacement)) {
                edits.Add(new Edit(shaderAt, 4 + Encoding.UTF8.GetByteCount(shader), replacement));
            }

            offset += 6;
            if (!TryReadInt(data, ref offset, out var properties) || properties < 0) {
                return false;
            }

            for (var p = 0; p < properties; p++) {
                if (!TrySkipString(data, ref offset)) {
                    return false;
                }

                offset += 40;
            }

            if (!TryReadInt(data, ref offset, out var mappings) || mappings < 0) {
                return false;
            }

            for (var m = 0; m < mappings; m++) {
                if (!TrySkipString(data, ref offset)) {
                    return false;
                }

                offset += 4;
                if (!TrySkipString(data, ref offset)) {
                    return false;
                }
            }
        }

        return offset <= data.Length;
    }

    private static byte[] Apply(byte[] data, List<Edit> edits)
    {
        var extra = 0;
        foreach (var edit in edits) {
            extra += Encoding.UTF8.GetByteCount(edit.Name) + 4 - edit.Remove;
        }

        var result = new byte[data.Length + extra];
        var source = 0;
        var dest = 0;
        foreach (var edit in edits) {
            var copy = edit.Offset - source;
            Buffer.BlockCopy(data, source, result, dest, copy);
            dest += copy;
            var name = Encoding.UTF8.GetBytes(edit.Name);
            result[dest] = (byte)name.Length;
            result[dest + 1] = (byte)(name.Length >> 8);
            result[dest + 2] = (byte)(name.Length >> 16);
            result[dest + 3] = (byte)(name.Length >> 24);
            Buffer.BlockCopy(name, 0, result, dest + 4, name.Length);
            dest += 4 + name.Length;
            source = edit.Offset + edit.Remove;
        }

        Buffer.BlockCopy(data, source, result, dest, data.Length - source);
        return result;
    }

    private static bool TryReadInt(byte[] data, ref int offset, out int value)
    {
        value = 0;
        if (offset < 0 || offset > data.Length - 4) {
            return false;
        }

        value = data[offset]
            | (data[offset + 1] << 8)
            | (data[offset + 2] << 16)
            | (data[offset + 3] << 24);
        offset += 4;
        return true;
    }

    private static bool TryReadString(byte[] data, ref int offset, out string value)
    {
        value = "";
        if (!TryReadInt(data, ref offset, out var length) || length < 0 || offset > data.Length - length) {
            return false;
        }

        value = Encoding.UTF8.GetString(data, offset, length);
        offset += length;
        return true;
    }

    private static bool TrySkipString(byte[] data, ref int offset)
    {
        if (!TryReadInt(data, ref offset, out var length) || length < 0 || offset > data.Length - length) {
            return false;
        }

        offset += length;
        return true;
    }

    private readonly record struct Edit(int Offset, int Remove, string Name);
}
