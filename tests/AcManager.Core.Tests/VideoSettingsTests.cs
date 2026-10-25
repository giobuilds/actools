using AcManager.Core.Settings;
using Xunit;

namespace AcManager.Core.Tests;

public class VideoSettingsTests
{
    [Fact]
    public void Write_updates_resolution_and_keeps_the_rest_of_video_ini()
    {
        var documents = Directory.CreateTempSubdirectory("actools-video-").FullName;
        try {
            var path = VideoSettingsFile.PathFor(documents);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            const string original = "[VIDEO]\r\nFULLSCREEN=1\r\nANISOTROPIC=8\r\nAASAMPLES=2\r\nWIDTH=1920\r\nHEIGHT=1080\r\n\r\n[EFFECTS]\r\nFXAA=0\t\t\t\t; doesn't work with OCULUS\r\n";
            File.WriteAllText(path, original);

            var written = VideoSettingsFile.Write(documents, new VideoSettings(2560, 1440, false));

            Assert.Equal(path, written);
            Assert.Equal(original, File.ReadAllText(path + ".bak"));
            Assert.Equal(
                "[VIDEO]\r\nFULLSCREEN=0\r\nANISOTROPIC=8\r\nAASAMPLES=2\r\nWIDTH=2560\r\nHEIGHT=1440\r\n\r\n[EFFECTS]\r\nFXAA=0\t\t\t\t; doesn't work with OCULUS\r\n",
                File.ReadAllText(path));
            var loaded = VideoSettingsFile.Read(documents);
            Assert.Equal(new VideoSettings(2560, 1440, false), loaded);
        } finally {
            Directory.Delete(documents, true);
        }
    }

    [Fact]
    public void Read_uses_defaults_when_video_ini_is_missing()
    {
        var documents = Directory.CreateTempSubdirectory("actools-video-missing-").FullName;
        try {
            Assert.Equal(VideoSettings.Default, VideoSettingsFile.Read(documents));
            VideoSettingsFile.Write(documents, new VideoSettings(1280, 720, true));
            Assert.Equal(
                "[VIDEO]\r\nFULLSCREEN=1\r\nWIDTH=1280\r\nHEIGHT=720\r\n",
                File.ReadAllText(VideoSettingsFile.PathFor(documents)));
        } finally {
            Directory.Delete(documents, true);
        }
    }
}
