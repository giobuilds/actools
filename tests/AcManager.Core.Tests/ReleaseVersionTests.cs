using AcManager.Core.Updates;
using Xunit;

namespace AcManager.Core.Tests;

public class ReleaseVersionTests
{
    [Fact]
    public void Newer_stable_tag_wins_over_drafts_and_prereleases()
    {
        const string json = """
            [
              {"tag_name":"v1.4.1","draft":false,"prerelease":false},
              {"tag_name":"v1.10.0","draft":false,"prerelease":false},
              {"tag_name":"v2.0.0","draft":true,"prerelease":false},
              {"tag_name":"v3.0.0","draft":false,"prerelease":true},
              {"tag_name":"not-a-version","draft":false,"prerelease":false}
            ]
            """;

        Assert.True(ReleaseVersions.TryNewerStable("1.4.0", json, out var newer));
        Assert.Equal("1.10.0", newer);
    }

    [Fact]
    public void The_current_version_has_no_newer_release()
    {
        const string json = """
            [
              {"tag_name":"v1.4.0","draft":false,"prerelease":false},
              {"tag_name":"1.3.0","draft":false,"prerelease":false}
            ]
            """;

        Assert.True(ReleaseVersions.TryNewerStable("v1.4.0", json, out var newer));
        Assert.Null(newer);
    }

    [Fact]
    public void A_payload_that_is_not_a_release_list_is_rejected()
    {
        Assert.False(ReleaseVersions.TryNewerStable("1.4.0", """{"message":"Not Found"}""", out _));
        Assert.ThrowsAny<System.Text.Json.JsonException>(() => ReleaseVersions.TryNewerStable("1.4.0", "{", out _));
    }
}
