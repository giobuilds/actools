using AcManager.Core.Content;
using StringBasedFilter;
using Xunit;

namespace AcManager.Core.Tests;

public class TrackSearchTests
{
    private static readonly TrackSummary Monza = new(
        "ks_monza",
        "Monza",
        [
            new TrackLayout(
                "",
                "Monza",
                City: "Monza",
                SpecsLength: "5793",
                SpecsWidth: "12",
                Country: "italy",
                Year: 1922,
                Tags: ["circuit"],
                GeoTags: new GeoTags("45.5", "9.2")),
        ]);

    [Fact]
    public void Plain_text_matches_the_folder_id_and_a_layout_name()
    {
        Assert.True(Matches("monza", Monza));
        Assert.True(Matches("ks_monza", Monza));
        Assert.False(Matches("spa", Monza));
        Assert.False(Matches("italy", Monza));
        Assert.False(Matches("5793", Monza));

        var layouts = new TrackSummary("ks_brands", "Brands Hatch", [
            new TrackLayout("", "Brands Hatch GP"),
            new TrackLayout("indy", "Brands Hatch Indy", City: "Fawkham"),
        ]);
        Assert.True(Matches("indy", layouts));
        Assert.True(Matches("gp", layouts));
        Assert.True(Matches("hatch", layouts));
        Assert.True(Matches("ks_brands", layouts));
        Assert.False(Matches("fawkham", layouts));
        Assert.False(Matches("id:indy", layouts));
        Assert.True(Matches("id:ks_brands", layouts));
        Assert.True(Matches("name:indy", layouts));
        Assert.True(Matches("layouts>1", layouts));
        Assert.False(Matches("layouts>1", Monza));
    }

    [Fact]
    public void Keys_follow_the_content_manager_names()
    {
        Assert.True(Matches("city:monza", Monza));
        Assert.True(Matches("country:italy", Monza));
        Assert.True(Matches("c:Italy", Monza));
        Assert.True(Matches("year:1922", Monza));
        Assert.True(Matches("y>1900", Monza));
        Assert.False(Matches("year>0", new TrackSummary("cup", "Cup", [new TrackLayout("", "Cup")])));
        Assert.True(Matches("tag:circuit", Monza));
        Assert.True(Matches("#circuit", Monza));
        Assert.False(Matches("tag:drift", Monza));
        Assert.True(Matches("geotags:45", Monza));
        Assert.False(Matches("kunos+", Monza));
        Assert.True(Matches("kunos-", Monza));
        Assert.False(Matches("author:kunos", Monza));

        var mod = new TrackSummary("mod_spa", "Spa", [
            new TrackLayout(
                "",
                "Spa",
                Version: "1.2",
                Description: "Cup notes",
                Author: "Reboot Team",
                Url: "https://example.com/spa",
                SpecsPitboxes: "about 20",
                Country: "Belgium"),
            new TrackLayout("gp", "Grand Prix", Country: "Italy", Year: 2018, Tags: ["endurance"]),
        ]);
        Assert.True(Matches("author:reboot", mod));
        Assert.True(Matches("description:notes", mod));
        Assert.True(Matches("desc:cup", mod));
        Assert.False(Matches("notes", mod));
        Assert.True(Matches("version:1.2", mod));
        Assert.False(Matches("1.2", mod));
        Assert.True(Matches("url:https://example.com/spa", mod));
        Assert.False(Matches("url:example.com", mod));
        Assert.False(Matches("example.com", mod));
        Assert.True(Matches("country:italy", mod));
        Assert.True(Matches("country:belgium", mod));
        Assert.True(Matches("year:2018", mod));
        Assert.True(Matches("#endurance", mod));
        Assert.True(Matches("pits>10", mod));
        Assert.True(Matches("pitboxes:20", mod));
        Assert.False(Matches("pits>0", Monza));
        Assert.False(Matches("pitboxes:2", Monza));

        var parsed = Filter.Create(TrackSummaryTester.Instance, "(");
        Assert.True(parsed.Test(Monza));
    }

    [Fact]
    public void Length_is_kilometres_and_width_is_metres()
    {
        Assert.True(Matches("length>5", Monza));
        Assert.True(Matches("len<6", Monza));
        Assert.False(Matches("length<5", Monza));
        Assert.True(Matches("length<4mi", Monza));
        Assert.False(Matches("length>4mi", Monza));
        Assert.True(Matches("length>5000m", Monza));
        Assert.False(Matches("length<5000m", Monza));
        Assert.True(Matches("width>10", Monza));
        Assert.True(Matches("width<15", Monza));
        Assert.False(Matches("width>20", Monza));

        var labelled = new TrackSummary("cup", "Cup", [
            new TrackLayout("", "Cup", SpecsLength: "5.793 km", SpecsWidth: "12 m"),
        ]);
        Assert.True(Matches("length>5", labelled));
        Assert.True(Matches("length<6", labelled));
        Assert.True(Matches("width>10", labelled));

        var dotted = new TrackSummary("laguna", "Laguna", [
            new TrackLayout("", "Laguna", SpecsLength: "3.602"),
        ]);
        Assert.True(Matches("length>3", dotted));
        Assert.True(Matches("length<4", dotted));
        Assert.False(Matches("length>4", dotted));

        var shortDecimal = new TrackSummary("short", "Short", [
            new TrackLayout("", "Short", SpecsLength: "5.79"),
        ]);
        Assert.False(Matches("length>1", shortDecimal));
        Assert.True(Matches("length<1", shortDecimal));

        var miles = new TrackSummary("miles", "Miles", [
            new TrackLayout("", "Miles", SpecsLength: "3 mi"),
        ]);
        Assert.True(Matches("length>4", miles));
        Assert.False(Matches("length>5", miles));

        var missing = new TrackSummary("plain", "Plain", [new TrackLayout("", "Plain")]);
        Assert.True(Matches("length<1", missing));
        Assert.False(Matches("length>0", missing));
        Assert.False(Matches("width<100", missing));
        Assert.False(Matches("width>0", missing));

        var wide = new TrackSummary("wide", "Wide", [
            new TrackLayout("", "Wide", SpecsWidth: "1 km"),
        ]);
        Assert.True(Matches("width>500", wide));
        Assert.True(Matches("width<1500", wide));
    }

    private static bool Matches(string query, TrackSummary track) =>
        Filter.Create(TrackSummaryTester.Instance, query).Test(track);
}
