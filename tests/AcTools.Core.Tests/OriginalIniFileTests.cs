using AcTools.DataFile;
using Xunit;

namespace AcTools.Core.Tests;

public class OriginalIniFileTests
{
    [Fact]
    public void Normal_parse_keeps_urls_and_cuts_brackets_and_semicolons()
    {
        var ini = IniFile.Parse("[A]\nURL=https://example.test\nNOTE=b;c\nNAME=foo[bar]\n");

        Assert.Equal("https://example.test", ini["A"].GetPossiblyEmpty("URL"));
        Assert.Equal("b", ini["A"].GetPossiblyEmpty("NOTE"));
        Assert.Equal("foo", ini["A"].GetPossiblyEmpty("NAME"));
        Assert.True(ini.ContainsKey("bar"));
    }

    [Fact]
    public void Square_brackets_within_keeps_brackets_in_the_value()
    {
        var ini = IniFile.Parse("[A]\nNAME=foo[bar]\n", IniFileMode.SquareBracketsWithin);

        Assert.Equal("foo[bar]", ini["A"].GetPossiblyEmpty("NAME"));
        Assert.False(ini.ContainsKey("bar"));
    }

    [Fact]
    public void Values_with_semicolons_keeps_the_semicolon()
    {
        var ini = IniFile.Parse("[A]\nNOTE=b;c\n", IniFileMode.ValuesWithSemicolons);

        Assert.Equal("b;c", ini["A"].GetPossiblyEmpty("NOTE"));
    }

    [Fact]
    public void Comments_mode_stores_the_trailing_note()
    {
        var ini = IniFile.Parse("[A]\nCARS=1 ; cars\n", IniFileMode.Comments);

        Assert.Equal("1", ini["A"].GetPossiblyEmpty("CARS"));
        Assert.Equal("cars", ini["A"].Commentaries["CARS"]);
    }

    [Fact]
    public void Comment_keeping_save_changes_one_value_and_keeps_crlf()
    {
        const string original = "[RACE]\r\nTRACK=ks_silverstone ; keep\r\nCARS=1\r\nMODEL=ks_ferrari_458\r\n";
        var root = Directory.CreateTempSubdirectory("actools-inifile-comments-").FullName;
        try {
            var path = Path.Combine(root, "race.ini");
            File.WriteAllText(path, original);

            var ini = new IniFile(path, IniFileMode.Comments);
            Assert.Equal("keep", ini["RACE"].Commentaries["TRACK"]);
            ini["RACE"].Set("CARS", 2);
            ini.Save(path);

            Assert.Equal(
                "[RACE]\r\nTRACK=ks_silverstone ; keep\r\nCARS=2\r\nMODEL=ks_ferrari_458\r\n",
                File.ReadAllText(path));
            Assert.False(File.Exists(Path.Combine(root, "race-1.ini")));
        } finally {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void Save_with_backup_leaves_a_numbered_copy()
    {
        const string original = "[RACE]\r\nTRACK=ks_silverstone ; keep\r\nCARS=1\r\nMODEL=ks_ferrari_458\r\n";
        var root = Directory.CreateTempSubdirectory("actools-inifile-backup-").FullName;
        try {
            var path = Path.Combine(root, "race.ini");
            File.WriteAllText(path, original);

            var ini = new IniFile(path, IniFileMode.Comments);
            ini["RACE"].Set("CARS", 2);
            ini.Save(path, true);

            Assert.Equal(original, File.ReadAllText(Path.Combine(root, "race-1.ini")));
            Assert.Equal(
                "[RACE]\r\nTRACK=ks_silverstone ; keep\r\nCARS=2\r\nMODEL=ks_ferrari_458\r\n",
                File.ReadAllText(path));
        } finally {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void Normal_save_with_backup_keeps_the_previous_file()
    {
        var root = Directory.CreateTempSubdirectory("actools-inifile-normal-").FullName;
        try {
            var path = Path.Combine(root, "race.ini");
            File.WriteAllText(path, "[RACE]\nCARS=1\n");

            var ini = new IniFile(path);
            ini["RACE"].Set("CARS", 2);
            ini.Save(path, true);

            Assert.Equal("[RACE]\nCARS=1\n", File.ReadAllText(Path.Combine(root, "race-1.ini")));
            Assert.Contains("CARS=2", File.ReadAllText(path), StringComparison.Ordinal);
            Assert.False(File.Exists(Path.Combine(root, "race.tmp-1.ini")));
        } finally {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void Get_double_accepts_a_comma_decimal()
    {
        var ini = IniFile.Parse("[A]\nVALUE=1,25\n");

        Assert.Equal(1.25, ini["A"].GetDouble("VALUE", 0));
    }

    [Fact]
    public void Write_changes_one_key_and_leaves_the_comment()
    {
        const string original = "[RACE]\r\nTRACK=ks_silverstone ; keep\r\nCARS=1\r\nMODEL=ks_ferrari_458\r\n";
        var root = Directory.CreateTempSubdirectory("actools-inifile-write-").FullName;
        try {
            var path = Path.Combine(root, "race.ini");
            File.WriteAllText(path, original);

            IniFile.Write(path, "RACE", "CARS", 2);

            Assert.Equal(
                "[RACE]\r\nTRACK=ks_silverstone ; keep\r\nCARS=2\r\nMODEL=ks_ferrari_458\r\n",
                File.ReadAllText(path));
        } finally {
            Directory.Delete(root, true);
        }
    }
}
