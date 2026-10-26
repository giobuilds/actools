using System.Runtime.Versioning;
using AcManager.Core.Packaging;
using Xunit;

namespace AcManager.Core.Tests;

public class AppDirTests
{
    [Fact]
    [SupportedOSPlatform("linux")]
    public void AppDir_contains_the_program_desktop_file_and_licence()
    {
        var root = Directory.CreateTempSubdirectory("actools-appdir-").FullName;
        try {
            var publish = Path.Combine(root, "publish");
            Directory.CreateDirectory(publish);
            File.WriteAllText(Path.Combine(publish, "ContentManager"), "bin");
            File.WriteAllText(Path.Combine(publish, "AcBridge.exe"), "exe");
            File.WriteAllText(Path.Combine(publish, "libSkiaSharp.so"), "so");
            var license = Path.Combine(root, "LICENSE.txt");
            var notice = Path.Combine(root, "NOTICE.txt");
            var icon = Path.Combine(root, "icon.png");
            File.WriteAllText(license, "Microsoft Public License");
            File.WriteAllText(notice, "Avalonia");
            File.WriteAllBytes(icon, [1, 2, 3]);

            var code = AppDirLayout.Run([
                "--publish", publish,
                "--appdir", Path.Combine(root, "AppDir"),
                "--license", license,
                "--notice", notice,
                "--icon", icon,
            ]);
            Assert.Equal(0, code);

            var appDir = Path.Combine(root, "AppDir");
            var desktop = File.ReadAllText(Path.Combine(appDir, AppDirLayout.AppId + ".desktop"));
            Assert.Contains("Name=Content Manager for Linux", desktop, StringComparison.Ordinal);
            Assert.Contains("Categories=Game;Utility;", desktop, StringComparison.Ordinal);
            Assert.Contains("Exec=ContentManager", desktop, StringComparison.Ordinal);
            Assert.DoesNotContain("\r", desktop, StringComparison.Ordinal);
            Assert.Equal(desktop, File.ReadAllText(Path.Combine(appDir, "usr", "share", "applications", AppDirLayout.AppId + ".desktop")));
            Assert.Equal("bin", File.ReadAllText(Path.Combine(appDir, "usr", "bin", "ContentManager")));
            Assert.Equal("exe", File.ReadAllText(Path.Combine(appDir, "usr", "bin", "AcBridge.exe")));
            Assert.Equal("so", File.ReadAllText(Path.Combine(appDir, "usr", "bin", "libSkiaSharp.so")));
            Assert.Equal("Microsoft Public License", File.ReadAllText(Path.Combine(appDir, "usr", "bin", "LICENSE.txt")));
            Assert.Equal("Avalonia", File.ReadAllText(Path.Combine(appDir, "usr", "share", "doc", "content-manager", "NOTICE.txt")));
            Assert.Equal(AppDirLayout.UpdateInformation, File.ReadAllText(Path.Combine(appDir, "update-information")).Trim());
            Assert.Equal(new byte[] { 1, 2, 3 }, File.ReadAllBytes(Path.Combine(appDir, AppDirLayout.AppId + ".png")));
            Assert.True(File.GetUnixFileMode(Path.Combine(appDir, "AppRun")).HasFlag(UnixFileMode.UserExecute));
            Assert.True(File.GetUnixFileMode(Path.Combine(appDir, "usr", "bin", "ContentManager")).HasFlag(UnixFileMode.UserExecute));
            Assert.Contains("exec \"$HERE/usr/bin/ContentManager\"", File.ReadAllText(Path.Combine(appDir, "AppRun")), StringComparison.Ordinal);
        } finally {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void Pack_command_rejects_a_missing_publish_directory()
    {
        var root = Directory.CreateTempSubdirectory("actools-appdir-missing-").FullName;
        try {
            var code = AppDirLayout.Run([
                "--publish", Path.Combine(root, "missing"),
                "--appdir", Path.Combine(root, "AppDir"),
                "--license", Path.Combine(root, "LICENSE.txt"),
                "--notice", Path.Combine(root, "NOTICE.txt"),
                "--icon", Path.Combine(root, "icon.png"),
            ]);
            Assert.Equal(1, code);
        } finally {
            Directory.Delete(root, true);
        }
    }
}
