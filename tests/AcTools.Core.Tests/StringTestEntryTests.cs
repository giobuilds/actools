using StringBasedFilter.TestEntries;
using Xunit;

namespace AcTools.Core.Tests;

public class StringTestEntryTests
{
    [Fact]
    public void Included_within_matches_a_word_and_complete_match_does_not()
    {
        var included = new StringTestEntry("aBc", StringMatchMode.IncludedWithin, true);
        var complete = new StringTestEntry("aBc", StringMatchMode.CompleteMatch, true);

        Assert.True(included.Test("abc"));
        Assert.True(included.Test("qw abc"));
        Assert.True(included.Test("qw ABCdef"));
        Assert.False(included.Test("qwABC"));

        Assert.True(complete.Test("abc"));
        Assert.False(complete.Test("qw abc"));
        Assert.False(complete.Test("qw ABCdef"));
        Assert.False(complete.Test("qwABC"));
    }
}
