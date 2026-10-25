using AcTools.Core.Data;
using Xunit;

namespace AcTools.Core.Tests;

public class IniTextTests
{
    [Fact]
    public void Set_values_changes_only_the_named_keys_and_keeps_comments()
    {
        const string original = "[VIDEO]\r\nFULLSCREEN=1\r\nANISOTROPIC=8\r\nWIDTH=1920\r\nHEIGHT=1080\r\n\r\n[CAMERA]\r\nMODE=DEFAULT\t\t; DEFAULT | TRIPLE\r\n";
        var updated = IniText.SetValues(original, "VIDEO", [
            new KeyValuePair<string, string>("FULLSCREEN", "0"),
            new KeyValuePair<string, string>("WIDTH", "2560"),
            new KeyValuePair<string, string>("HEIGHT", "1440"),
        ]);

        Assert.Equal(
            "[VIDEO]\r\nFULLSCREEN=0\r\nANISOTROPIC=8\r\nWIDTH=2560\r\nHEIGHT=1440\r\n\r\n[CAMERA]\r\nMODE=DEFAULT\t\t; DEFAULT | TRIPLE\r\n",
            updated);
    }

    [Fact]
    public void Set_values_inserts_a_missing_key_under_the_section_header()
    {
        const string original = "[VIDEO]\r\nFULLSCREEN=1\r\nANISOTROPIC=8\r\n";
        var updated = IniText.SetValues(original, "VIDEO", [
            new KeyValuePair<string, string>("FULLSCREEN", "1"),
            new KeyValuePair<string, string>("WIDTH", "1920"),
            new KeyValuePair<string, string>("HEIGHT", "1080"),
        ]);

        Assert.Equal("[VIDEO]\r\nWIDTH=1920\r\nHEIGHT=1080\r\nFULLSCREEN=1\r\nANISOTROPIC=8\r\n", updated);
    }

    [Fact]
    public void Set_values_keeps_a_trailing_comment_on_the_edited_line()
    {
        const string original = "[VIDEO]\r\nWIDTH=1920\t\t; pixels\r\n";
        var updated = IniText.SetValues(original, "VIDEO", [
            new KeyValuePair<string, string>("WIDTH", "1280"),
        ]);

        Assert.Equal("[VIDEO]\r\nWIDTH=1280\t\t; pixels\r\n", updated);
    }
}
