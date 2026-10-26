#nullable disable
using StringBasedFilter;
using StringBasedFilter.TestEntries;

namespace AcManager.Core.Content;

/// <summary>
/// Car search for the fields this app already scans. Plain text matches id, name, brand, and class.
/// <c>brand:</c> and <c>class:</c> follow Content Manager's car filter keys.
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
            default:
                return false;
        }
    }
}
