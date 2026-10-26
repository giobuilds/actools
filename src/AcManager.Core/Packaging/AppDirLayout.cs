namespace AcManager.Core.Packaging;

public sealed record AppDirSpec(
    string PublishDirectory,
    string AppDir,
    string LicensePath,
    string NoticePath,
    string IconPath);

/// <summary>
/// AppDir for the linux-x64 build. appimagetool reads update-information.
/// </summary>
public static class AppDirLayout
{
    public const string AppId = "io.github.giobuilds.actools";

    public const string DesktopName = "Content Manager for Linux";

    public const string ExecutableName = "ContentManager";

    public const string UpdateInformation =
        "gh-releases-zsync|giobuilds|actools|latest|ContentManager-*_x86_64.AppImage.zsync";

    public static string DesktopFile() =>
        "[Desktop Entry]\n"
        + "Type=Application\n"
        + "Name=" + DesktopName + "\n"
        + "Comment=Launch Assetto Corsa sessions under Proton\n"
        + "Exec=" + ExecutableName + "\n"
        + "Icon=" + AppId + "\n"
        + "Categories=Game;Utility;\n"
        + "Terminal=false\n"
        + "StartupWMClass=ContentManager\n";

    public static string AppRunScript() =>
        "#!/bin/sh\n"
        + "set -eu\n"
        + "HERE=$(dirname \"$(readlink -f \"$0\")\")\n"
        + "export PATH=\"$HERE/usr/bin:$PATH\"\n"
        + "export LD_LIBRARY_PATH=\"$HERE/usr/bin${LD_LIBRARY_PATH:+:$LD_LIBRARY_PATH}\"\n"
        + "cd \"$HERE/usr/bin\"\n"
        + "exec \"$HERE/usr/bin/" + ExecutableName + "\" \"$@\"\n";

    public static int Run(IReadOnlyList<string> args)
    {
        string? publish = null;
        string? appDir = null;
        string? license = null;
        string? notice = null;
        string? icon = null;
        for (var i = 0; i < args.Count; i++) {
            var arg = args[i];
            if (i + 1 >= args.Count) {
                Console.Error.WriteLine("Missing value for " + arg);
                return 1;
            }

            var value = args[++i];
            switch (arg) {
                case "--publish":
                    publish = value;
                    break;
                case "--appdir":
                    appDir = value;
                    break;
                case "--license":
                    license = value;
                    break;
                case "--notice":
                    notice = value;
                    break;
                case "--icon":
                    icon = value;
                    break;
                default:
                    Console.Error.WriteLine("Unknown argument " + arg);
                    return 1;
            }
        }

        if (publish == null || appDir == null || license == null || notice == null || icon == null) {
            Console.Error.WriteLine(
                "Usage: --pack-appdir --publish DIR --appdir DIR --license FILE --notice FILE --icon FILE");
            return 1;
        }

        try {
            Write(new AppDirSpec(publish, appDir, license, notice, icon));
        } catch (Exception ex) when (ex is IOException or ArgumentException) {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }

        return 0;
    }

    public static void Write(AppDirSpec spec)
    {
        if (!Directory.Exists(spec.PublishDirectory)) {
            throw new DirectoryNotFoundException(spec.PublishDirectory);
        }

        var bin = Path.Combine(spec.AppDir, "usr", "bin");
        Directory.CreateDirectory(bin);
        foreach (var file in Directory.EnumerateFiles(spec.PublishDirectory, "*", SearchOption.AllDirectories)) {
            var relative = Path.GetRelativePath(spec.PublishDirectory, file);
            var destination = Path.Combine(bin, relative);
            var parent = Path.GetDirectoryName(destination);
            if (!string.IsNullOrEmpty(parent)) {
                Directory.CreateDirectory(parent);
            }

            File.Copy(file, destination, overwrite: true);
        }

        var appRun = Path.Combine(spec.AppDir, "AppRun");
        File.WriteAllText(appRun, AppRunScript());
        MakeExecutable(appRun);
        MakeExecutable(Path.Combine(bin, ExecutableName));

        var desktop = DesktopFile();
        File.WriteAllText(Path.Combine(spec.AppDir, AppId + ".desktop"), desktop);
        var applications = Path.Combine(spec.AppDir, "usr", "share", "applications");
        Directory.CreateDirectory(applications);
        File.WriteAllText(Path.Combine(applications, AppId + ".desktop"), desktop);

        var iconName = AppId + ".png";
        File.Copy(spec.IconPath, Path.Combine(spec.AppDir, iconName), overwrite: true);
        File.Copy(spec.IconPath, Path.Combine(spec.AppDir, ".DirIcon"), overwrite: true);
        var icons = Path.Combine(spec.AppDir, "usr", "share", "icons", "hicolor", "256x256", "apps");
        Directory.CreateDirectory(icons);
        File.Copy(spec.IconPath, Path.Combine(icons, iconName), overwrite: true);

        var doc = Path.Combine(spec.AppDir, "usr", "share", "doc", "content-manager");
        Directory.CreateDirectory(doc);
        File.Copy(spec.LicensePath, Path.Combine(bin, "LICENSE.txt"), overwrite: true);
        File.Copy(spec.LicensePath, Path.Combine(doc, "LICENSE.txt"), overwrite: true);
        File.Copy(spec.NoticePath, Path.Combine(bin, "NOTICE.txt"), overwrite: true);
        File.Copy(spec.NoticePath, Path.Combine(doc, "NOTICE.txt"), overwrite: true);
        File.WriteAllText(Path.Combine(spec.AppDir, "update-information"), UpdateInformation + "\n");
    }

    private static void MakeExecutable(string path)
    {
        if (!File.Exists(path)) {
            return;
        }

        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS()) {
            return;
        }

        var mode = File.GetUnixFileMode(path);
        File.SetUnixFileMode(
            path,
            mode | UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute);
    }
}
