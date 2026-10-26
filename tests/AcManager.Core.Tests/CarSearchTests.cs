using AcManager.Core.Content;
using StringBasedFilter;
using Xunit;

namespace AcManager.Core.Tests;

public class CarSearchTests
{
    private static readonly CarSummary Mazda = new(
        "ks_mazda_mx5_cup",
        "Mazda MX-5 Cup",
        "Mazda",
        "street",
        []);

    private static readonly CarSummary Ferrari = new(
        "ks_ferrari_458",
        "Ferrari 458 Italia",
        "Ferrari",
        "street",
        []);

    [Fact]
    public void Plain_text_matches_name_brand_and_id_on_word_boundaries()
    {
        Assert.True(Matches("mazda", Mazda));
        Assert.True(Matches("cup", Mazda));
        Assert.True(Matches("ks_mazda", Mazda));
        Assert.False(Matches("ferrari", Mazda));
        Assert.False(Matches("x5", Mazda));
    }

    [Fact]
    public void Keys_and_boolean_queries_follow_the_filter_language()
    {
        Assert.True(Matches("brand:mazda", Mazda));
        Assert.False(Matches("brand:ferrari", Mazda));
        Assert.True(Matches("class:street", Mazda));
        Assert.True(Matches("ferrari | mazda", Mazda));
        Assert.True(Matches("ferrari | mazda", Ferrari));
        Assert.False(Matches("!mazda", Mazda));
        Assert.True(Matches("brand:ferrari & class:street", Ferrari));
        Assert.False(Matches("brand:ferrari & class:race", Ferrari));
    }

    [Fact]
    public void An_unfinished_parenthesis_does_not_throw()
    {
        var parsed = Filter.Create(CarSummaryTester.Instance, "(");
        Assert.True(parsed.Test(Mazda));
    }

    private static bool Matches(string query, CarSummary car) =>
        Filter.Create(CarSummaryTester.Instance, query).Test(car);
}
