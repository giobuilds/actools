using AcTools.Platform.Linux;
using Xunit;

namespace AcManager.Core.Tests;

public class SteamLoginUsersTests
{
    [Fact]
    public void Read_most_recent_id_returns_the_account_marked_recent()
    {
        const string vdf =
            "\"users\"\n{\n\t\"76561198000000000\"\n\t{\n\t\t\"AccountName\"\t\t\"old\"\n\t\t\"MostRecent\"\t\t\"0\"\n\t}\n"
            + "\t\"76561198000000001\"\n\t{\n\t\t\"MostRecent\"\t\t\"1\"\n\t\t\"PersonaName\"\t\t\"Driver\"\n\t}\n}\n";

        Assert.Equal("76561198000000001", SteamLoginUsers.ReadMostRecentId(vdf));
    }

    [Fact]
    public void Read_most_recent_id_ignores_a_file_with_no_recent_account()
    {
        const string vdf =
            "\"users\"\n{\n\t\"76561198000000000\"\n\t{\n\t\t\"MostRecent\"\t\t\"0\"\n\t}\n\t\"short\"\n\t{\n\t\t\"MostRecent\"\t\t\"1\"\n\t}\n}\n";

        Assert.Null(SteamLoginUsers.ReadMostRecentId(vdf));
        Assert.Null(SteamLoginUsers.ReadMostRecentId(""));
        Assert.Null(SteamLoginUsers.ReadMostRecentId("not a vdf"));
    }

    [Fact]
    public void Read_most_recent_id_uses_autologin_when_most_recent_is_absent()
    {
        const string vdf =
            "\"users\"\n{\n\t\"76561198000000001\"\n\t{\n\t\t\"AccountName\"\t\t\"driver\"\n"
            + "\t\t\"PersonaName\"\t\t\"Driver\"\n\t\t\"RememberPassword\"\t\t\"1\"\n\t\t\"AutoLogin\"\t\t\"1\"\n\t}\n}\n";

        Assert.Equal("76561198000000001", SteamLoginUsers.ReadMostRecentId(vdf));
        Assert.Equal("Driver", SteamLoginUsers.ReadAccount(vdf)?.PersonaName);
    }

    [Fact]
    public void Read_most_recent_id_uses_autologin_when_most_recent_is_zero()
    {
        const string vdf =
            "\"users\"\n{\n\t\"76561198000000006\"\n\t{\n\t\t\"MostRecent\"\t\t\"0\"\n\t\t\"AutoLogin\"\t\t\"1\"\n\t}\n}\n";

        Assert.Equal("76561198000000006", SteamLoginUsers.ReadMostRecentId(vdf));
    }

    [Fact]
    public void Read_most_recent_id_uses_the_only_unmarked_account()
    {
        const string vdf = "\"users\"\n{\n\t\"76561198000000002\"\n\t{\n\t\t\"AccountName\"\t\t\"solo\"\n\t}\n}\n";

        Assert.Equal("76561198000000002", SteamLoginUsers.ReadMostRecentId(vdf));
    }

    [Fact]
    public void Read_most_recent_id_does_not_guess_between_unmarked_accounts()
    {
        const string vdf =
            "\"users\"\n{\n\t\"76561198000000002\"\n\t{\n\t\t\"AccountName\"\t\t\"a\"\n\t}\n"
            + "\t\"76561198000000003\"\n\t{\n\t\t\"AccountName\"\t\t\"b\"\n\t}\n}\n";

        Assert.Null(SteamLoginUsers.ReadMostRecentId(vdf));
    }

    [Fact]
    public void Read_most_recent_id_prefers_most_recent_over_autologin()
    {
        const string vdf =
            "\"users\"\n{\n\t\"76561198000000004\"\n\t{\n\t\t\"AutoLogin\"\t\t\"1\"\n\t\t\"PersonaName\"\t\t\"Other\"\n\t}\n"
            + "\t\"76561198000000005\"\n\t{\n\t\t\"MostRecent\"\t\t\"1\"\n\t\t\"PersonaName\"\t\t\"Driver\"\n\t}\n}\n";

        var account = SteamLoginUsers.ReadAccount(vdf);
        Assert.Equal("76561198000000005", account?.Id);
        Assert.Equal("Driver", account?.PersonaName);
    }

    [Fact]
    public void Candidate_paths_follow_each_steam_root()
    {
        var paths = SteamLoginUsers.CandidatePaths().ToArray();
        Assert.Contains(paths, path => path.Replace('\\', '/').EndsWith(".local/share/Steam/config/loginusers.vdf", StringComparison.Ordinal));
        Assert.Contains(paths, path => path.Replace('\\', '/').EndsWith(".var/app/com.valvesoftware.Steam/data/Steam/config/loginusers.vdf", StringComparison.Ordinal));
        Assert.Contains(paths, path => path.Replace('\\', '/').EndsWith(".var/app/com.valvesoftware.Steam/.local/share/Steam/config/loginusers.vdf", StringComparison.Ordinal));
        Assert.Contains(paths, path => path.Replace('\\', '/').EndsWith("snap/steam/common/.local/share/Steam/config/loginusers.vdf", StringComparison.Ordinal));
    }
}
