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

    [Fact]
    public void Json_fields_use_the_content_manager_keys()
    {
        var car = new CarSummary(
            "mod_rx7",
            "Mazda RX-7",
            "Mazda",
            "street",
            [],
            ["jdm", "tuned"],
            "Japan",
            1997,
            "ks_mazda_rx7",
            "Reboot Team",
            "250",
            "300nm",
            "1250",
            "250",
            "5.2",
            "5");

        Assert.True(Matches("tag:jdm", car));
        Assert.True(Matches("#tuned", car));
        Assert.False(Matches("tag:drift", car));
        Assert.True(Matches("country:japan", car));
        Assert.True(Matches("c:Japan", car));
        Assert.True(Matches("year:1997", car));
        Assert.True(Matches("y>1990", car));
        Assert.False(Matches("year:1997", Mazda));
        Assert.False(Matches("year>0", Mazda));
        Assert.True(Matches("parent:ks_mazda_rx7", car));
        Assert.True(Matches("author:reboot", car));
        Assert.False(Matches("kunos+", car));
        var official = car with { Author = CarAuthors.Kunos };
        Assert.True(Matches("kunos+", official));
        Assert.False(Matches("kunos-", official));
        Assert.True(Matches("kunos-", car));
        Assert.True(Matches("author:kunos", official));
        Assert.True(Matches("bhp>200", car));
        Assert.False(Matches("power>300", car));
        Assert.True(Matches("torque>250", car));
        Assert.True(Matches("weight:1250", car));
        Assert.True(Matches("mass<1300", car));
        Assert.True(Matches("acceleration<6", car));
        Assert.True(Matches("topspeed>200", car));
        Assert.True(Matches("speed:250", car));
        Assert.True(Matches("pw<6", car));
        Assert.True(Matches("pwratio:5", car));
    }

    private static bool Matches(string query, CarSummary car) =>
        Filter.Create(CarSummaryTester.Instance, query).Test(car);
}
