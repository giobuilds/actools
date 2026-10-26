#nullable disable

namespace StringBasedFilter.TestEntries {
    public interface ITestEntryFactory {
        ITestEntry Create(Operator op, string value);
    }
}
