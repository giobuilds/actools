#nullable disable
using System;
using StringBasedFilter.TestEntries;

namespace StringBasedFilter {
    public interface ITestEntry {
        /// <summary>
        /// Override this tester with a tester which will treat data in a different
        /// way. Used for numerial test entries to add postfixes support.
        /// </summary>
        void Set(ITestEntryFactory factory);

        bool Test(string value);
        bool Test(double value);
        bool Test(bool value);
        bool Test(TimeSpan value);
        bool Test(DateTime value);
    }
}
