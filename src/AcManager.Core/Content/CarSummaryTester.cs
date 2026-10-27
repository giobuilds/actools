#nullable disable
using StringBasedFilter;
using StringBasedFilter.TestEntries;

namespace AcManager.Core.Content;

/// <summary>
/// Car search for the fields this app scans from <c>ui_car.json</c>. Plain text matches id, name, brand, and class.
/// Key names follow Content Manager. <c>parent</c> compares the parent folder id. Power, torque, weight,
/// top speed, and acceleration read unit suffixes such as kW, ft-lb, t, mph, and ms. The default units are
/// bhp, Nm, kilograms, kilometres per hour, and seconds. <c>pw</c> compares the JSON text.
/// <c>maxrpm</c> reads the highest RPM on the torque curve. A query suffix <c>k</c> means thousands.
/// </summary>
public sealed class CarSummaryTester : ITester<CarSummary>
{
    public static readonly CarSummaryTester Instance = new CarSummaryTester();

    public string ParameterFromKey(string key)
    {
        switch (key) {
            case "id":
                return nameof(CarSummary.Id);
            case "name":
                return nameof(CarSummary.Name);
            case "b":
            case "brand":
                return nameof(CarSummary.Brand);
            case "class":
                return nameof(CarSummary.ClassName);
            case "t":
            case "tag":
                return nameof(CarSummary.Tags);
            case "c":
            case "country":
                return nameof(CarSummary.Country);
            case "y":
            case "year":
                return nameof(CarSummary.Year);
            case "parent":
                return nameof(CarSummary.ParentId);
            case "a":
            case "author":
            case "k":
            case "kunos":
                return nameof(CarSummary.Author);
            case "bhp":
            case "power":
                return nameof(CarSummary.SpecsBhp);
            case "torque":
                return nameof(CarSummary.SpecsTorque);
            case "weight":
            case "mass":
                return nameof(CarSummary.SpecsWeight);
            case "acceleration":
                return nameof(CarSummary.SpecsAcceleration);
            case "speed":
            case "topspeed":
                return nameof(CarSummary.SpecsTopSpeed);
            case "pw":
            case "pwratio":
                return nameof(CarSummary.SpecsPwRatio);
            case "maxrpm":
                return nameof(CarSummary.MaxRpm);
            case "version":
                return nameof(CarSummary.Version);
            case "desc":
            case "description":
                return nameof(CarSummary.Description);
            case "url":
                return nameof(CarSummary.Url);
            case null:
                return nameof(CarSummary.Name);
            default:
                return null;
        }
    }

    public bool Test(CarSummary car, string key, ITestEntry value)
    {
        switch (key) {
            case null:
                return value.Test(car.Id) || value.Test(car.Name) || value.Test(car.Brand) || value.Test(car.ClassName);
            case "id":
                return value.Test(car.Id);
            case "name":
                return value.Test(car.Name);
            case "b":
            case "brand":
                return value.Test(car.Brand);
            case "class":
                return value.Test(car.ClassName);
            case "t":
            case "tag":
                return car.Tags != null && car.Tags.Any(value.Test);
            case "c":
            case "country":
                return value.Test(car.Country);
            case "y":
            case "year":
                return car.Year.HasValue && value.Test(car.Year.Value);
            case "parent":
                return value.Test(car.ParentId);
            case "a":
            case "author":
                return value.Test(car.Author);
            case "k":
            case "kunos":
                return value.Test(car.Author == CarAuthors.Kunos);
            case "bhp":
            case "power":
                value.Set(CarSpecQuery.Power);
                return value.Test(car.SpecsBhp);
            case "torque":
                value.Set(CarSpecQuery.Torque);
                return value.Test(car.SpecsTorque);
            case "weight":
            case "mass":
                value.Set(CarSpecQuery.Weight);
                return value.Test(car.SpecsWeight);
            case "acceleration":
                value.Set(CarSpecQuery.Seconds);
                return value.Test(car.SpecsAcceleration);
            case "speed":
            case "topspeed":
                value.Set(CarSpecQuery.Speed);
                return value.Test(car.SpecsTopSpeed);
            case "pw":
            case "pwratio":
                return value.Test(car.SpecsPwRatio);
            case "maxrpm":
                value.Set(CarSpecQuery.Units);
                return value.Test(car.MaxRpm);
            case "version":
                return value.Test(car.Version);
            case "desc":
            case "description":
                return value.Test(car.Description);
            case "url":
                return value.Test(car.Url);
            default:
                return false;
        }
    }
}
