#nullable disable
using StringBasedFilter;
using StringBasedFilter.TestEntries;
using Xunit;
using System;
using System.Diagnostics;
using System.Linq;

namespace AcTools.Core.Tests {
    internal class ListTester : IParentTester<string[]> {
        public static readonly ListTester Instance = new ListTester();

        public string ParameterFromKey(string key) {
            return null;
        }

        public bool Test(string[] obj, string key, ITestEntry value) {
            switch (key) {
                case null:
                    return value.Test(string.Join(", ", obj));

                case "len":
                    return value.Test(obj.Length);

                case "empty":
                    return value.Test(obj.Length == 0);

                case "f":
                    return value.Test(obj.ElementAtOrDefault(0));

                case "s":
                    return value.Test(obj.ElementAtOrDefault(1));

                case "c":
                    return obj.Any(value.Test);
            }

            return false;
        }

        public bool TestChild(string[] obj, string key, IFilter filter) {
            switch (key) {
                case null:
                case "0":
                case "f":
                    return obj.Length > 0 && filter.Test(StringTester.Instance, obj[0]);

                case "1":
                case "s":
                    return obj.Length > 1 && filter.Test(StringTester.Instance, obj[1]);
            }

            return false;
        }
    }

        public class FilterTest {
        [Fact]
        public void ParsingTest() {
            var filter = Filter.Create(new StringTester(), "A&B");


            var s = filter.ToString();


            Assert.Equal(@"{ ""=a"" && ""=b"" }", s);
            Assert.True(filter.Test("A B"));
            Assert.True(filter.Test("a b"));
            Assert.False(filter.Test("A"));
            Assert.False(filter.Test("B"));
            Assert.False(filter.Test("qwerty"));
            Assert.False(filter.Test("AB"));
            Assert.False(filter.Test("ab"));
        }

        [Fact]
        public void ParsingBugTest() {
            var filter = Filter.Create(new StringTester(), "( A, B )");
            var s = filter.ToString();
            Assert.Equal(@"{ ""=a"" || ""=b"" }", s);
            Assert.True(filter.Test("A B"));
            Assert.True(filter.Test("a b"));
            Assert.True(filter.Test("A"));
            Assert.True(filter.Test("B"));
            Assert.False(filter.Test("qwerty"));
            Assert.True(filter.Test("AB"));
            Assert.True(filter.Test("ab"));

            var filter2 = Filter.Create(new StringTester(), " A, B ");
            Assert.Equal(@"{ ""=a"" || ""=b"" }", filter2.ToString());

            var filter3 = Filter.Create(new StringTester(), " A, B ( C, D )");
            Assert.Equal(@"{ ""=a"" || { ""=b"" && { ""=c"" || ""=d"" } } }", filter3.ToString());
        }

        [Fact]
        public void FilteringTestSpace() {
            var filter = Filter.Create(new StringTester(), "A B");


            var s = filter.ToString();


            Assert.True(filter.Test("A B"));
            Assert.True(filter.Test("A Q B"));
            Assert.True(filter.Test("a b"));
            Assert.True(filter.Test("q A B"));
            Assert.False(filter.Test("qA B"));
        }

        [Fact]
        public void ParsingTestSpace() {
            var filter = Filter.Create(new StringTester(), "A B | C");
            var s = filter.ToString();
            Assert.True(filter.Test("A B"));
            Assert.True(filter.Test("A Q B"));
            Assert.True(filter.Test("a b"));
            Assert.True(filter.Test("q A B"));
            Assert.False(filter.Test("qA B"));


            filter = Filter.Create(new StringTester(), "\"A B\" | C");
            s = filter.ToString();
            Assert.True(filter.Test("A B"));
            Assert.False(filter.Test("A Q B"));
            Assert.True(filter.Test("a b"));
            Assert.True(filter.Test("q A B"));
            Assert.False(filter.Test("qA B"));
        }

        [Fact]
        public void HyphenTest() {
            var filter = Filter.Create(new StringTester(), "A-B");
            var s = filter.ToString();
            Assert.True(filter.Test("A-BC"));
            Assert.False(filter.Test("A"));
            Assert.False(filter.Test("BC"));
            Assert.True(filter.Test("A A-B"));
            Assert.False(filter.Test("qA B"));
        }

        [Fact]
        public void QuotesTest() {
            var em0 = Filter.Create(new StringTester(), "!");
            Assert.False(em0.Test("!"));
            Assert.False(em0.Test("a!"));
            Assert.False(em0.Test("!a"));

            var ema = Filter.Create(new StringTester(), "\\!");
            Assert.True(ema.Test("!"));
            Assert.False(ema.Test("a!"));
            Assert.True(ema.Test("!a"));

            var em1 = Filter.Create(new StringTester(), "`!`");
            Assert.True(em1.Test("!"));
            Assert.True(em1.Test("a!"));
            Assert.True(em1.Test("!a"));

            var em2 = Filter.Create(new StringTester(), "'!'");
            Assert.True(em2.Test("!"));
            Assert.False(em2.Test("a!"));
            Assert.False(em2.Test("!a"));
        }

        [Fact]
        public void DevTest() {
            var filter = Filter.Create(new StringTester(), "A & B(Q)");


            var s = filter.ToString();
        }

        [Fact]
        public void StrictMatchTest() {
            var filterAny = Filter.Create(ListTester.Instance, "f:test");

            Assert.True(filterAny.Test(new[] { "test" }));
            Assert.True(filterAny.Test(new[] { "testb" }));
            Assert.True(filterAny.Test(new[] { "a testb" }));

            var filterStrict = Filter.Create(ListTester.Instance, "f:'test'");

            Assert.True(filterStrict.Test(new[] { "test" }));
            Assert.False(filterStrict.Test(new[] { "testb" }));
            Assert.False(filterStrict.Test(new[] { "atestb" }));

            var filterExclamationMarkAny = Filter.Create(ListTester.Instance, "f:\"!\"");

            Assert.True(filterExclamationMarkAny.Test(new[] { "!" }));
            Assert.True(filterExclamationMarkAny.Test(new[] { "!b" }));
            Assert.True(filterExclamationMarkAny.Test(new[] { "a !b" }));

            var filterExclamationMarkStrict = Filter.Create(ListTester.Instance, "f:'!'");

            Assert.True(filterExclamationMarkStrict.Test(new[] { "!" }));
            Assert.False(filterExclamationMarkStrict.Test(new[] { "!b" }));
            Assert.False(filterExclamationMarkStrict.Test(new[] { "a!b" }));
        }

        [Fact]
        public void ChildTest() {
            var filter = Filter.Create(ListTester.Instance, "len=2 & 1(A & B)");


            var s = filter.ToString();


            Assert.True(Filter.Create(ListTester.Instance, "len=2 & len<5").Test(new[] { "Q", "A B" }));

            Assert.True(filter.Test(new[] { "Q", "A B" }));
            Assert.True(filter.Test(new[] { "Q", "Aa BB" }));
            Assert.False(filter.Test(new[] { "Q", "Aa BB", "5" }));
            Assert.False(filter.Test(new[] { "Q", "AaBB" }));
        }

        [Fact]
        public void PriorityTest() {
            var w0 = Stopwatch.StartNew();
            var filter0 = Filter.Create(ListTester.Instance, "len=2 & 1(A & B) | ((0:A* ^ empty+) & 0(len > 1)) | 0(len=3)");
            var filter1 = Filter.Create(ListTester.Instance, "len=2 & 1(A & B) | (0:A* ^ empty+) & 0(len > 1) | 0(len=3)");
            Assert.Equal(filter0.ToString(), filter1.ToString());
        }

        [Fact]
        public void PerformanceTest() {
            var w0 = Stopwatch.StartNew();
            var filter = Filter.Create(ListTester.Instance, "len=2 & 1(A & B) | ((0:A* ^ empty+) & 0(len > 1)) | 0(len=3)");


            var s = filter.ToString();

            var d = new[] {
                new[] { "Q", "A B" },
                new[] { "Q", "Aa BB" },
                new[] { "Q", "Aa BB", "5" },
                new[] { "Q", "AaBB" },
                new[] { "QWE", "AaBB" }
            };

            var w = Stopwatch.StartNew();
            var m = 100000;
            var n = Enumerable.Range(0, m).Select(x => d[x % d.Length]).Count(filter.Test);
            Assert.Equal(n, m * 3 / 5);
        }

        [Fact]
        public void SubFilterDotTest() {
            var w0 = Stopwatch.StartNew();
            var filter = Filter.Create(ListTester.Instance, "len=2 & 1(A & B) | ((0:A* ^ empty+) & f.len > 1) | f.len=3");


            var s = filter.ToString();

            var d = new[] {
                new[] { "Q", "A B" },
                new[] { "Q", "Aa BB" },
                new[] { "Q", "Aa BB", "5" },
                new[] { "Q", "AaBB" },
                new[] { "QWE", "AaBB" }
            };

            var w = Stopwatch.StartNew();
            var m = 100000;
            var n = Enumerable.Range(0, m).Select(x => d[x % d.Length]).Count(filter.Test);
            Assert.Equal(n, m * 3 / 5);
        }

        [Fact]
        public void PropagateKeyTest_0() {
            var filter = Filter.Create(new ListTester(), "len<4 & !c:bmw lotus ferrari");
            Assert.Equal("{ { { \"len=<4\" && { ! \"c==bmw\" } } && { ! \"c==lotus\" } } && { ! \"c==ferrari\" } }", filter.ToString());

            Assert.True(filter.Test(new []{ "mini", "lada", "toyota" }));
            Assert.False(filter.Test(new []{ "audi", "bmw", "toyota" }));
            Assert.False(filter.Test(new []{ "lotus" }));
            Assert.False(filter.Test(new []{ "lotus", "ferrari" }));
            Assert.True(filter.Test(new []{ "mersedes", "aston martin", "maserati" }));
            Assert.False(filter.Test(new []{ "mersedes", "aston martin", "maserati", "vw" }));
        }

        [Fact]
        public void PropagateKeyTest_1() {
            var filter = Filter.Create(new ListTester(), "len<4 !c:bmw lotus ferrari");

            Assert.True(filter.Test(new []{ "mini", "lada", "toyota" }));
            Assert.False(filter.Test(new []{ "audi", "bmw", "toyota" }));
            Assert.False(filter.Test(new []{ "lotus" }));
            Assert.False(filter.Test(new []{ "lotus", "ferrari" }));
            Assert.True(filter.Test(new []{ "mersedes", "aston martin", "maserati" }));
            Assert.False(filter.Test(new []{ "mersedes", "aston martin", "maserati", "vw" }));
        }

        [Fact]
        public void QueryTest() {
            Assert.True(Filter.Create(new StringTester(), "ho*og").Test("hotdog"));
            Assert.True(Filter.Create(new StringTester(), "hot?og").Test("hotdog"));
            Assert.False(Filter.Create(new StringTester(), "hot.og").Test("hot,og"));
            Assert.False(Filter.Create(new StringTester(), @"hot\)\)\)").Test("hotdog"));
            Assert.False(Filter.Create(new StringTester(), @"hot\(\(\(").Test("hotdog"));
        }
    }
}
