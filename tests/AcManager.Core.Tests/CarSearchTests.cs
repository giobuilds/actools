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
        var versioned = car with { Name = "Cup", Version = "1.23" };
        Assert.True(Matches("version:1.23", versioned));
        Assert.False(Matches("v1.23", versioned));
        var described = car with { Name = "Cup", Description = "Cup notes v1.2" };
        Assert.True(Matches("description:notes", described));
        Assert.True(Matches("desc:v1.2", described));
        Assert.False(Matches("notes", described));
        Assert.False(Matches("description:mazda", Mazda));
        var linked = car with { Name = "Cup", Url = "https://example.com/cars/cup" };
        Assert.True(Matches("url:https://example.com/cars", linked));
        Assert.False(Matches("url:example.com", linked));
        Assert.False(Matches("example.com", linked));
        Assert.False(Matches("url:https://example.com", Mazda));
    }

    [Fact]
    public void Spec_queries_convert_unit_suffixes()
    {
        var car = SpecCar(bhp: "186 kW", torque: "221 ft-lb", weight: "1.2 t", topSpeed: "155 mph", acceleration: "5200 ms", pw: "5");
        Assert.True(Matches("power>249", car));
        Assert.False(Matches("power>250", car));
        Assert.True(Matches("bhp>249", car));
        Assert.False(Matches("power>200kw", car));
        Assert.True(Matches("power>180kw", car));
        Assert.True(Matches("torque>299", car));
        Assert.False(Matches("torque>300", car));
        Assert.False(Matches("torque>250ft-lb", car));
        Assert.True(Matches("mass>1100", car));
        Assert.True(Matches("weight<1300", car));
        Assert.False(Matches("mass>2t", car));
        Assert.True(Matches("mass>1t", car));
        Assert.True(Matches("topspeed>249", car));
        Assert.False(Matches("topspeed>250", car));
        Assert.False(Matches("topspeed>160mph", car));
        Assert.True(Matches("acceleration<6", car));
        Assert.False(Matches("acceleration>5", car));
        Assert.False(Matches("acceleration<1000ms", car));
        Assert.True(Matches("pw<6", car));
        Assert.True(Matches("pwratio:5", car));
        Assert.False(Matches("power>0", Mazda));

        var labelled = SpecCar(bhp: "650bhp", torque: "300nm", weight: "1250 kg", topSpeed: "250 km/h", acceleration: "5.2", pw: "5");
        Assert.True(Matches("power>600", labelled));
        Assert.False(Matches("power>700", labelled));
        Assert.True(Matches("torque>250", labelled));
        Assert.True(Matches("mass<1300", labelled));
        Assert.True(Matches("weight:1250", labelled));
        Assert.False(Matches("weight:1250", car));
        Assert.True(Matches("topspeed>249", labelled));
        Assert.False(Matches("topspeed>250", labelled));
        Assert.True(Matches("speed:250", labelled));
        Assert.True(Matches("acceleration<6", labelled));
        Assert.True(Matches("acceleration<5.1", labelled));
        Assert.False(Matches("acceleration<5", labelled));
        Assert.False(Matches("acceleration<0:05.1", labelled));
        Assert.True(Matches("acceleration=5", labelled));
        Assert.True(Matches("acceleration=0:05.7", labelled));
        Assert.False(Matches("acceleration=0:06.5", labelled));
        Assert.True(Matches("acceleration:5.2", labelled));
        Assert.False(Matches("acceleration:5.2", car));

        var pounds = SpecCar(weight: "2756 lb");
        Assert.True(Matches("mass>1250", pounds));
        Assert.True(Matches("mass<1251", pounds));

        Assert.True(Matches("mass>1000", SpecCar(weight: "1,250")));
        var grouped = SpecCar(weight: "1,250 kg");
        Assert.False(Matches("mass>1000", grouped));
        Assert.True(Matches("mass<2", grouped));
    }

    private static CarSummary SpecCar(
        string? bhp = null,
        string? torque = null,
        string? weight = null,
        string? topSpeed = null,
        string? acceleration = null,
        string? pw = null) =>
        new(
            "mod_spec",
            "Spec Car",
            "Spec",
            "street",
            [],
            SpecsBhp: bhp,
            SpecsTorque: torque,
            SpecsWeight: weight,
            SpecsTopSpeed: topSpeed,
            SpecsAcceleration: acceleration,
            SpecsPwRatio: pw);

    private static bool Matches(string query, CarSummary car) =>
        Filter.Create(CarSummaryTester.Instance, query).Test(car);
}
