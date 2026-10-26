#nullable disable

namespace StringBasedFilter {
    public interface ITester<in T> {
        /// <summary>
        /// Only changeable values!
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        string ParameterFromKey(string key);

        bool Test(T obj, string key, ITestEntry value);
    }

    public interface IParentTester<in T> : ITester<T> {
        bool TestChild(T obj, string key, IFilter filter);
    }
}
