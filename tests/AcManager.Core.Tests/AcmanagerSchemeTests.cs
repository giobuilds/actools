using AcTools.Platform.Linux;
using Xunit;

namespace AcManager.Core.Tests;

public class AcmanagerSchemeTests
{
    [Fact]
    public void Register_writes_the_desktop_entry_and_keeps_other_mime_apps()
    {
        var root = Directory.CreateTempSubdirectory("actools-scheme-").FullName;
        try {
            var applications = Path.Combine(root, "applications");
            var mimeapps = Path.Combine(root, "mimeapps.list");
            File.WriteAllText(mimeapps, "[Default Applications]\ntext/plain=editor.desktop\n\n[Added Associations]\ntext/plain=editor.desktop\n");

            AcmanagerScheme.Register("/home/keeper/Workspace/actools/ContentManager", applications, mimeapps);

            var desktop = File.ReadAllText(Path.Combine(applications, AcmanagerScheme.DesktopFileName));
            Assert.Contains("Exec=/home/keeper/Workspace/actools/ContentManager %u", desktop, StringComparison.Ordinal);
            Assert.Contains("MimeType=x-scheme-handler/acmanager;", desktop, StringComparison.Ordinal);
            var mime = File.ReadAllText(mimeapps);
            Assert.Contains("text/plain=editor.desktop", mime, StringComparison.Ordinal);
            Assert.Contains("x-scheme-handler/acmanager=content-manager.desktop", mime, StringComparison.Ordinal);
            Assert.Contains("[Added Associations]", mime, StringComparison.Ordinal);
        } finally {
            Directory.Delete(root, true);
        }
    }
}
