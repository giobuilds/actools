#nullable disable

namespace StringBasedFilter {
    public interface IFilter {
        string Source { get; }

        bool Test<T>(ITester<T> tester, T obj);

        bool IsAffectedBy<T>(ITester<T> tester, string propertyName);
    }

    public interface IFilter<in T> {
        string Source { get; }

        bool Test(T obj);

        bool IsAffectedBy(string propertyName);
    }
}
