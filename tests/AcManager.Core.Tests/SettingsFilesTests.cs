using AcManager.Core.Settings;
using Xunit;

namespace AcManager.Core.Tests;

public class SettingsFilesTests
{
    [Fact]
    public void Write_updates_assists_and_keeps_the_stability_comment()
    {
        var documents = Directory.CreateTempSubdirectory("actools-assists-").FullName;
        try {
            var path = AssistSettingsFile.PathFor(documents);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            const string original =
                "[ASSISTS]\r\nIDEAL_LINE=1\r\nAUTO_BLIP=1\r\nSTABILITY_CONTROL=100\t\t\t; 0-100 % of stability control\r\nAUTO_BRAKE=0\r\nAUTO_SHIFTER=1\r\nABS=2\r\nTRACTION_CONTROL=2\r\nAUTO_CLUTCH=1\r\nVISUALDAMAGE=0\r\nDAMAGE=0\r\nFUEL_RATE=0\r\nTYRE_WEAR=1\r\nTYRE_BLANKETS=1\r\nSLIPSTREAM=1\r\nCUSTOM=keep";
            File.WriteAllText(path, original);

            var loaded = AssistSettingsFile.Read(documents);
            Assert.True(loaded.IdealLine);
            Assert.Equal(100, loaded.StabilityControl);
            Assert.Equal(2, loaded.Abs);
            Assert.Equal(0, loaded.Damage);

            var written = AssistSettingsFile.Write(documents, loaded with { IdealLine = false, StabilityControl = 40, FuelRate = 80 });

            Assert.Equal(path, written);
            Assert.Equal(original, File.ReadAllText(path + ".bak"));
            Assert.Equal(
                "[ASSISTS]\r\nIDEAL_LINE=0\r\nAUTO_BLIP=1\r\nSTABILITY_CONTROL=40\t\t\t; 0-100 % of stability control\r\nAUTO_BRAKE=0\r\nAUTO_SHIFTER=1\r\nABS=2\r\nTRACTION_CONTROL=2\r\nAUTO_CLUTCH=1\r\nVISUALDAMAGE=0\r\nDAMAGE=0\r\nFUEL_RATE=50\r\nTYRE_WEAR=1\r\nTYRE_BLANKETS=1\r\nSLIPSTREAM=1\r\nCUSTOM=keep",
                File.ReadAllText(path));
        } finally {
            Directory.Delete(documents, true);
        }
    }

    [Fact]
    public void Write_stores_audio_as_fractions_and_keeps_other_sections()
    {
        var documents = Directory.CreateTempSubdirectory("actools-audio-").FullName;
        try {
            var path = AudioSettingsFile.PathFor(documents);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            const string original =
                "[HEADER]\r\nVERSION=1\r\n\r\n[LEVELS]\r\nMASTER=1\r\nENGINE=1.0\r\nWIND=0.4\r\nTYRES=0.5\r\nOPPONENTS=0.8\r\nMUSIC=0.5\r\nUISOUNDS=0.7\r\nSURFACES=0.9\r\nDIRT_BOTTOM=1\r\n\r\n[SETTINGS]\r\n\r\n[SKIDS]\r\nENTRY_POINT=100\r\n";
            File.WriteAllText(path, original);

            var loaded = AudioSettingsFile.Read(documents);
            Assert.Equal(100, loaded.Master);
            Assert.Equal(100, loaded.Engine);
            Assert.Equal(40, loaded.Wind);
            Assert.Equal(50, loaded.Tyres);

            AudioSettingsFile.Write(documents, loaded with { Wind = 25, Master = 150 });

            Assert.Equal(original, File.ReadAllText(path + ".bak"));
            Assert.Equal(
                "[HEADER]\r\nVERSION=1\r\n\r\n[LEVELS]\r\nMASTER=1\r\nENGINE=1\r\nWIND=0.25\r\nTYRES=0.5\r\nOPPONENTS=0.8\r\nMUSIC=0.5\r\nUISOUNDS=0.7\r\nSURFACES=0.9\r\nDIRT_BOTTOM=1\r\n\r\n[SETTINGS]\r\n\r\n[SKIDS]\r\nENTRY_POINT=100\r\n",
                File.ReadAllText(path));
            Assert.DoesNotContain("BRAKES", File.ReadAllText(path), StringComparison.Ordinal);
        } finally {
            Directory.Delete(documents, true);
        }
    }

    [Fact]
    public void Write_updates_force_feedback_and_keeps_bindings()
    {
        var documents = Directory.CreateTempSubdirectory("actools-controls-").FullName;
        try {
            var path = ControlSettingsFile.PathFor(documents);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            const string original =
                "[HEADER]\r\nINPUT_METHOD=X360\r\n\r\n[CONTROLLERS]\r\nPGUID0=abc\r\nCON1=ODDOR-handbrake\r\nCON2=\r\nCON0=Logitech G29 Driving Force Racing Wheel\r\n\r\n[STEER]\r\nLOCK=899\r\nFF_GAIN=1\r\nSTEER_GAMMA=1\r\n\r\n[GEAR_UP]\r\nJOY=0\r\nBUTTON=12\r\n\r\n[FF_TWEAKS]\r\nMIN_FF=0.11\r\n";
            File.WriteAllText(path, original);

            var loaded = ControlSettingsFile.Read(documents);
            Assert.Equal("X360", loaded.InputMethod);
            Assert.Equal(
                [
                    new NamedController(0, "Logitech G29 Driving Force Racing Wheel"),
                    new NamedController(1, "ODDOR-handbrake"),
                ],
                loaded.Controllers);
            Assert.Equal(100, loaded.ForceFeedback);
            Assert.Equal(899, loaded.SteerLock);

            ControlSettingsFile.Write(documents, loaded with { ForceFeedback = 40, SteerLock = 10 });

            Assert.Equal(original, File.ReadAllText(path + ".bak"));
            Assert.Equal(
                "[HEADER]\r\nINPUT_METHOD=X360\r\n\r\n[CONTROLLERS]\r\nPGUID0=abc\r\nCON1=ODDOR-handbrake\r\nCON2=\r\nCON0=Logitech G29 Driving Force Racing Wheel\r\n\r\n[STEER]\r\nLOCK=40\r\nFF_GAIN=0.4\r\nSTEER_GAMMA=1\r\n\r\n[GEAR_UP]\r\nJOY=0\r\nBUTTON=12\r\n\r\n[FF_TWEAKS]\r\nMIN_FF=0.11\r\n",
                File.ReadAllText(path));
        } finally {
            Directory.Delete(documents, true);
        }
    }

    [Fact]
    public void Read_lists_python_apps_and_write_toggles_active()
    {
        var root = Directory.CreateTempSubdirectory("actools-python-").FullName;
        try {
            var documents = Path.Combine(root, "documents");
            var game = Path.Combine(root, "game");
            Directory.CreateDirectory(Path.Combine(documents, "cfg"));
            Directory.CreateDirectory(Path.Combine(game, "apps", "python", "Chat"));
            Directory.CreateDirectory(Path.Combine(game, "apps", "python", "gMeter"));
            Directory.CreateDirectory(Path.Combine(game, "apps", "python", "system"));
            Directory.CreateDirectory(Path.Combine(game, "apps", "python", "NewLap"));
            Directory.CreateDirectory(Path.Combine(game, "apps", "python", "A=B"));
            var path = PythonAppsFile.PathFor(documents);
            const string original =
                "[CHAT]\r\nACTIVE=1\r\nPOS=1\r\n\r\n[NOTES]\r\nINFO=keep\r\n\r\n[ACNOTIFY]\r\nACTIVE=0\r\n\r\n[GMETER]\r\nACTIVE=1\r\n";
            File.WriteAllText(path, original);

            var loaded = PythonAppsFile.Read(documents, game);
            Assert.Equal(
                [
                    new PythonAppSetting("CHAT", "Chat", true),
                    new PythonAppSetting("ACNOTIFY", "ACNOTIFY", false),
                    new PythonAppSetting("GMETER", "gMeter", true),
                    new PythonAppSetting("NEWLAP", "NewLap", false),
                ],
                loaded);

            var saved = loaded.Select(app => app.Section == "CHAT" ? app with { Active = false }
                : app.Section == "NEWLAP" ? app with { Active = true }
                : app).ToArray();
            saved = [.. saved, new PythonAppSetting("A=B", "A=B", true)];
            PythonAppsFile.Write(documents, saved);

            Assert.Equal(original, File.ReadAllText(path + ".bak"));
            Assert.Equal(
                "[CHAT]\r\nACTIVE=0\r\nPOS=1\r\n\r\n[NOTES]\r\nINFO=keep\r\n\r\n[ACNOTIFY]\r\nACTIVE=0\r\n\r\n[GMETER]\r\nACTIVE=1\r\n\r\n[NEWLAP]\r\nACTIVE=1\r\n",
                File.ReadAllText(path));
        } finally {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void Missing_files_use_defaults_and_create_only_the_edited_keys()
    {
        var documents = Directory.CreateTempSubdirectory("actools-cfg-missing-").FullName;
        try {
            Assert.Equal(AssistSettings.Default, AssistSettingsFile.Read(documents));
            Assert.Equal(AudioSettings.Default, AudioSettingsFile.Read(documents));
            Assert.Equal(ControlSettings.Default, ControlSettingsFile.Read(documents));
            Assert.Empty(PythonAppsFile.Read(documents, null));

            AssistSettingsFile.Write(documents, AssistSettings.Default);
            AudioSettingsFile.Write(documents, AudioSettings.Default with { Master = 40 });
            ControlSettingsFile.Write(documents, ControlSettings.Default with { ForceFeedback = 40, SteerLock = 2000 });

            Assert.Equal(
                "[ASSISTS]\r\nIDEAL_LINE=0\r\nAUTO_BLIP=0\r\nSTABILITY_CONTROL=0\r\nAUTO_BRAKE=0\r\nAUTO_SHIFTER=0\r\nABS=1\r\nTRACTION_CONTROL=1\r\nAUTO_CLUTCH=0\r\nVISUALDAMAGE=1\r\nDAMAGE=100\r\nFUEL_RATE=1\r\nTYRE_WEAR=1\r\nTYRE_BLANKETS=0\r\nSLIPSTREAM=1\r\n",
                File.ReadAllText(AssistSettingsFile.PathFor(documents)));
            Assert.Equal(
                "[LEVELS]\r\nMASTER=0.4\r\nENGINE=1\r\nTYRES=0.8\r\nWIND=0.9\r\nOPPONENTS=0.9\r\nMUSIC=0.5\r\nUISOUNDS=0.7\r\nSURFACES=1\r\nDIRT_BOTTOM=1\r\n",
                File.ReadAllText(AudioSettingsFile.PathFor(documents)));
            Assert.Equal(
                "[STEER]\r\nFF_GAIN=0.4\r\nLOCK=1080\r\n",
                File.ReadAllText(ControlSettingsFile.PathFor(documents)));
            Assert.DoesNotContain("INPUT_METHOD", File.ReadAllText(ControlSettingsFile.PathFor(documents)), StringComparison.Ordinal);
        } finally {
            Directory.Delete(documents, true);
        }
    }
}
