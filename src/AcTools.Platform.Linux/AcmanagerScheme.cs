namespace AcTools.Platform.Linux;

/// <summary>
/// Registers this process as the handler for acmanager:// links.
/// Browsers open the ACS Drift Join button with that scheme.
/// </summary>
public static class AcmanagerScheme
{
    public const string DesktopFileName = "content-manager.desktop";

    public const string MimeType = "x-scheme-handler/acmanager";

    public static string DesktopEntry(string executable)
    {
        return "[Desktop Entry]\n"
            + "Name=Content Manager\n"
            + "Exec=" + QuoteExec(executable) + " %u\n"
            + "Type=Application\n"
            + "Terminal=false\n"
            + "NoDisplay=true\n"
            + "MimeType=" + MimeType + ";\n";
    }

    public static void RegisterCurrentProcess()
    {
        var executable = Environment.ProcessPath;
        if (string.IsNullOrEmpty(executable)) {
            return;
        }

        var name = Path.GetFileName(executable);
        if (name.Equals("dotnet", StringComparison.OrdinalIgnoreCase)) {
            return;
        }

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (string.IsNullOrEmpty(home)) {
            return;
        }

        var data = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
        if (string.IsNullOrWhiteSpace(data)) {
            data = Path.Combine(home, ".local", "share");
        }

        var config = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
        if (string.IsNullOrWhiteSpace(config)) {
            config = Path.Combine(home, ".config");
        }

        Register(executable, Path.Combine(data, "applications"), Path.Combine(config, "mimeapps.list"));
    }

    public static void Register(string executable, string applicationsDirectory, string mimeappsFile)
    {
        Directory.CreateDirectory(applicationsDirectory);
        var desktopPath = Path.Combine(applicationsDirectory, DesktopFileName);
        var entry = DesktopEntry(executable);
        if (!File.Exists(desktopPath) || File.ReadAllText(desktopPath) != entry) {
            File.WriteAllText(desktopPath, entry);
        }

        WriteMimeapps(mimeappsFile);
    }

    private static void WriteMimeapps(string path)
    {
        var lines = File.Exists(path) ? File.ReadAllLines(path).ToList() : [];
        var assignment = MimeType + "=" + DesktopFileName;
        var section = -1;
        var replaced = false;
        for (var i = 0; i < lines.Count; i++) {
            var trimmed = lines[i].Trim();
            if (trimmed.StartsWith('[') && trimmed.EndsWith(']')) {
                if (section >= 0) {
                    lines.Insert(i, assignment);
                    replaced = true;
                    break;
                }

                if (trimmed.Equals("[Default Applications]", StringComparison.OrdinalIgnoreCase)) {
                    section = i;
                }

                continue;
            }

            if (section >= 0 && trimmed.StartsWith(MimeType + "=", StringComparison.OrdinalIgnoreCase)) {
                lines[i] = assignment;
                replaced = true;
                break;
            }
        }

        if (!replaced && section >= 0) {
            lines.Insert(section + 1, assignment);
        } else if (!replaced) {
            if (lines.Count > 0 && lines[^1].Length > 0) {
                lines.Add("");
            }

            lines.Add("[Default Applications]");
            lines.Add(assignment);
        }

        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory)) {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllLines(path, lines);
    }

    private static string QuoteExec(string executable)
    {
        if (executable.IndexOfAny([' ', '\t', '"', '\\']) < 0) {
            return executable;
        }

        return "\"" + executable.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";
    }
}
