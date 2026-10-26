#nullable disable
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using StringBasedFilter.TestEntries;
using StringBasedFilter.Utils;

namespace StringBasedFilter.Parsing {
    public enum FilterComparingOperation {
        IsSame = ':',
        IsTrue = '+',
        IsFalse = '-',
        MoreThan = '>',
        LessThan = '<',
        MoreThanOrEqualTo = '≥',
        LessThanOrEqualTo = '≤',
        EqualTo = '=',
        SimilarTo = '≈',
    }

    public static class FilterComparingOperations {
        public static FilterComparingOperation Parse(string key) {
            if (key?.Length == 1 && Enum.IsDefined(typeof(FilterComparingOperation), (int)key[0])) {
                return (FilterComparingOperation)key[0];
            }

            switch (key) {
                case "−":
                    return FilterComparingOperation.IsFalse;
                case ">=":
                case "=>":
                    return FilterComparingOperation.MoreThanOrEqualTo;
                case "<=":
                case "=<":
                    return FilterComparingOperation.LessThanOrEqualTo;
                default:
                    return FilterComparingOperation.IsSame;
            }
        }
    }

    public class FilterPropertyValue {
        public string ChildKey { get; set; }

        public string PropertyKey { get; }

        public FilterComparingOperation ComparingOperation { get; }

        public string PropertyValue { get; }

        public FilterPropertyValue(string propertyKey, FilterComparingOperation comparingOperation, string propertyValue) {
            PropertyKey = propertyKey;
            ComparingOperation = comparingOperation;
            PropertyValue = propertyValue;
        }

        public FilterPropertyValue(string propertyKey, FilterComparingOperation comparingOperation) {
            PropertyKey = propertyKey;
            ComparingOperation = comparingOperation;
            PropertyValue = "";
        }
    }

    public delegate Regex RegexFactory(string query, StringMatchMode mode);

    public delegate FilterPropertyValue ValueSplitFunc(string value);

    public delegate ITestEntry BooleanTestFactory(bool value);

    public delegate ITestEntry CustomTestEntryFactory(string value);

    public delegate string CustomValueParser(string data, ref int index);

    public delegate ITestEntry ExtraTestEntryFactory(string key);

    public delegate string ValueConversion(string rawValue);

    public class FilterParams {
        /// <summary>
        /// Params used by default, changeable.
        /// </summary>
        public static readonly FilterParams Default = new FilterParams();

        /// <summary>
        /// For more strict use.
        /// </summary>
        public static readonly FilterParams DefaultStrictNoChildKeys = new FilterParams {
            ValueSplitter = new ValueSplitter(s => {
                var match = Regex.Match(s, @"^([a-zA-Z_]+)\s*((?:>=|<=|=>|=<|[:<>≥≤=≈])\s*|[+\-−]\s*$)");
                return !match.Success ? null : new FilterPropertyValue(match.Groups[1].Value,
                        FilterComparingOperations.Parse(match.Groups[2].Value.TrimEnd()), s.Substring(match.Length).TrimStart());
            }, ':', '<', '>', '≥', '≤', '=', '≈', '+', '-', '−'),
            CaseInvariant = false,
        };

        /// <summary>
        /// What it says.
        /// </summary>
        public bool CaseInvariant { get; set; } = true;

        /// <summary>
        /// What it says.
        /// </summary>
        public bool TreatSpacesAsAndOperands { get; set; } = true;

        /// <summary>
        /// How to compare strings with queries.
        /// </summary>
        public StringMatchMode StringMatchMode { get; set; } = StringMatchMode.IncludedWithin;

        /// <summary>
        /// Converts input value before splitting if needed. Can be null.
        /// </summary>
        public ValueConversion ValueConversion { get; set; } =
            s => s.StartsWith("#") ? "tag:" + s.Substring(1) : s;

        /// <summary>
        /// Either splits value to three parts: child key (optional), property key and comparing operation
        /// or returns null for simple mode without key.
        /// </summary>
        public ValueSplitter ValueSplitter { get; set; } = ValueSplitter.Default;

        /// <summary>
        /// Defines how to build Regex objects from queries.
        /// </summary>
        public RegexFactory RegexFactory { get; set; } = RegexFromQuery.Create;

        /// <summary>
        /// Default factory for boolean testers, override to make auto-conversion more strict/flexible.
        /// </summary>
        public BooleanTestFactory BooleanTestFactory { get; set; } = b => new BooleanTestEntry(b);

        /*/// <summary>
        /// Factory for extra TestEntry objects, for testing stuff like distances or weights.
        /// </summary>
        public ExtraTestEntryFactory ExtraTestEntryFactory { get; set; } = key => null;*/

        public CustomTestEntryFactory CustomTestEntryFactory { get; set; }

        public CustomValueParser CustomValueParser { get; set; }
    }

    internal static class DefaultValueSplitFunc {
        private static readonly Regex ParsingRegex = new Regex(@"^([a-zA-Z]+)(\.[a-zA-Z]+)?\s*((?:>=|<=|=>|=<|[:<>≥≤=≈])\s*|[+\-−]\s*$)", RegexOptions.Compiled);
        public static readonly char[] Separators = { ':', '<', '>', '≥', '≤', '=', '≈', '+', '-', '−' };

        public static FilterPropertyValue Default(string s) {
            var match = ParsingRegex.Match(s);
            if (!match.Success) return null;

            var key = match.Groups[1].Value.ToLower();
            var operation = FilterComparingOperations.Parse(match.Groups[3].Value.TrimEnd());
            var value = s.Substring(match.Length).TrimStart();

            if (match.Groups[2].Success) {
                var actualKey = match.Groups[2].Value.Substring(1).ToLower();
                return new FilterPropertyValue(actualKey, operation, value) { ChildKey = key };
            }

            return new FilterPropertyValue(key, operation, value);
        }
    }

    public class ValueSplitter {
        public ValueSplitter(ValueSplitFunc valueSplitFunc, params char[] keywordSplitCharacters) {
            ValueSplitFunc = valueSplitFunc;
            KeywordSplitCharacters = keywordSplitCharacters;
        }

        public ValueSplitFunc ValueSplitFunc { get; }

        public char[] KeywordSplitCharacters { get; }

        public static ValueSplitter Default { get; } = new ValueSplitter(DefaultValueSplitFunc.Default, DefaultValueSplitFunc.Separators);
    }

    internal class FilterParser {
        private readonly FilterParams _params;

        private int _pos;
        private string _filter;

        private List<string> _properties;

        public FilterParser(FilterParams filterParams) {
            _params = filterParams ?? FilterParams.Default;
        }

        internal FilterTreeNode Parse(string filter, out string[] properties) {
            _pos = 0;
            _filter = filter;
            _properties = new List<string>();
            var result = NextNode();
            properties = _properties.ToArray();
            return result;
        }

        private static bool IsTwoOperandsControlChar(char c) {
            return c == '&' || c == '|' || c == ',' || c == '^';
        }

        private static bool IsNonValueChar(char c) {
            return !IsTwoOperandsControlChar(c) && c != ')' && c != 0;
        }

        private bool IsKeywordSeparatorChar(char c) {
            return Array.IndexOf(_params.ValueSplitter.KeywordSplitCharacters, c) != -1;
        }

        private static bool IsControlChar(char c) {
            return IsTwoOperandsControlChar(c);
        }

        private char GetNextNonSpace(out int pos) {
            pos = _pos;
            for (; pos < _filter.Length; pos++) {
                if (char.IsWhiteSpace(_filter[pos])) continue;
                return _filter[pos];
            }

            return (char)0;
        }

        private bool NextMatchIs(char match) {
            if (GetNextNonSpace(out var pos) != match) return false;
            _pos = pos + 1;
            return true;
        }

        private FilterTreeNode NextNode() {
            return NextNodeOr();
        }

        private FilterTreeNode NextNodeOr() {
            var node = NextNodeNor();
            var previousNode = node;
            var nodeKeySet = false;
            string nodeKey = null;
            var nodeKeyNot = false;

            while (true) {
                if (NextMatchIs('|')) {
                    previousNode = NextNodeNor();
                    nodeKeySet = false;
                    node = new FilterTreeNodeOr(node, previousNode);
                } else if (NextMatchIs(',')) {
                    var b = NextNodeNor();

                    if (!nodeKeySet) {
                        nodeKeyNot = previousNode is FilterTreeNodeNot;
                        nodeKey = ((nodeKeyNot ? ((FilterTreeNodeNot)previousNode).A : previousNode) as FilterTreeNodeValue)?.Key;
                        nodeKeySet = nodeKey != null;
                    }

                    if (nodeKey != null) {
                        if (b is FilterTreeNodeValue bv && bv.Key == null) {
                            bv.Key = nodeKey;
                            if (nodeKeyNot) {
                                b = new FilterTreeNodeNot(b);
                            }
                        } else {
                            previousNode = b;
                            nodeKeySet = false;
                        }
                    }

                    node = new FilterTreeNodeOr(node, b);
                } else {
                    return node;
                }
            }
        }

        private FilterTreeNode NextNodeNor() {
            var node = NextNodeAnd();
            while (NextMatchIs('^')) {
                node = new FilterTreeNodeNor(node, NextNodeAnd());
            }
            return node;
        }

        private FilterTreeNode NextNodeAnd() {
            var node = NextNodeNot();
            var previousNode = node;
            var nodeKeySet = false;
            string nodeKey = null;
            var nodeKeyNot = false;

            while (true) {
                if (NextMatchIs('&')) {
                    previousNode = NextNodeNot();
                    nodeKeySet = false;
                    node = new FilterTreeNodeAnd(node, previousNode);
                } else if (_params.TreatSpacesAsAndOperands && IsNonValueChar(GetNextNonSpace(out _)) && _filter.Length > _pos && _filter[_pos] == ' ') {
                    var b = NextNodeNot();

                    if (!nodeKeySet) {
                        nodeKeyNot = previousNode is FilterTreeNodeNot;
                        nodeKey = ((nodeKeyNot ? ((FilterTreeNodeNot)previousNode).A : previousNode) as FilterTreeNodeValue)?.Key;
                        nodeKeySet = nodeKey != null;
                    }

                    if (nodeKey != null) {
                        if (b is FilterTreeNodeValue bv && bv.Key == null) {
                            bv.Key = nodeKey;
                            if (nodeKeyNot) {
                                b = new FilterTreeNodeNot(b);
                            }
                        } else {
                            previousNode = b;
                            nodeKeySet = false;
                        }
                    }

                    node = new FilterTreeNodeAnd(node, b);
                } else {
                    return node;
                }
            }
        }

        private FilterTreeNode NextNodeNot() {
            var inverse = false;
            while (NextMatchIs('!')) {
                inverse = !inverse;
            }
            return inverse ? new FilterTreeNodeNot(NextNodeValue()) : NextNodeValue();
        }

        private void SkipSpaces() {
            for (; _pos < _filter.Length && _filter[_pos] == ' '; _pos++) { }
        }

        private FilterTreeNode NextNodeValue() {
            string s = null;
            FilterTreeNode node = null;

            SkipSpaces();

            var previousNonSpace = (char)0;
            var previousNonSpaceIndex = -1;
            var keywordSeparatorIncluded = false;

            var parsed = _params.CustomValueParser?.Invoke(_filter, ref _pos);
            if (parsed != null) {
                node = FilterTreeNodeValue.Create(parsed, _params, out s);
            } else {
                var buffer = new StringBuilder(_filter.Length - _pos);
                for (; _pos < _filter.Length; _pos++) {
                    var c = _filter[_pos];
                    if (c == '\\') {
                        _pos++;
                        if (_pos < _filter.Length) {
                            var e = _filter[_pos];
                            if (RegexFromQuery.IsQuerySymbol(e)) {
                                buffer.Append('\\');
                            }

                            buffer.Append(e);
                        }
                        continue;
                    }

                    if (!_params.TreatSpacesAsAndOperands || c != ' ') {
                        previousNonSpace = c;
                        previousNonSpaceIndex = _pos;
                    } else {
                        var nextNonSpace = GetNextNonSpace(out var nonSpacePos);
                        if (IsControlChar(nextNonSpace)) break;
                        if (IsKeywordSeparatorChar(nextNonSpace) || IsKeywordSeparatorChar(previousNonSpace)) {
                            _pos = nonSpacePos - 1;
                            continue;
                        }

                        // Complex and questionable case: ignore spaces when they separate a number from a word, but only then
                        if (keywordSeparatorIncluded && previousNonSpaceIndex >= 0 && char.IsDigit(previousNonSpace) && char.IsLetter(nextNonSpace)) {
                            for (var j = previousNonSpaceIndex - 1; j >= 0; j--) {
                                var p = _filter[j];
                                if (char.IsLetter(p)) goto BreakPiece;
                                if (!char.IsDigit(p)) break;
                            }

                            for (var j = nonSpacePos + 1; j < _filter.Length; j++) {
                                var p = _filter[j];
                                if (char.IsDigit(p)) goto BreakPiece;
                                if (!char.IsLetter(p)) break;
                            }

                            _pos = nonSpacePos - 1;
                            continue;
                        }

                        BreakPiece:
                        break;
                    }

                    if (IsControlChar(c) || c == ')') {
                        break;
                    }

                    if (IsKeywordSeparatorChar(c)) {
                        keywordSeparatorIncluded = true;
                    }

                    if (c == '`' || c == '"' || c == '\'') {
                        var i = _pos + 1;
                        var literal = new StringBuilder(_filter.Length - _pos);
                        literal.Append(c);

                        for (; i < _filter.Length; i++) {
                            var n = _filter[i];
                            if (n == '\\' && (c != '`' || i + 1 < _filter.Length && _filter[i + 1] == '`')) {
                                i++;
                                if (i < _filter.Length) {
                                    literal.Append(_filter[i]);
                                }
                                continue;
                            }

                            literal.Append(n);

                            if (n == c) {
                                // If there is only one quote symbol, it’s not going to get processed as quoted text
                                buffer.Append(literal);
                                _pos = i;
                                break;
                            }
                        }
                    } else if (c == '(') {
                        var value = buffer.ToString().Trim();

                        if (value.Length > 0) {
                            var oldProperties = _properties;
                            _properties = new List<string>();

                            _pos++;
                            node = NextNode();
                            _pos++;

                            node = new FilterTreeNodeChild(value, node, _params);
                            _properties = oldProperties;
                            s = value;
                        } else {
                            _pos++;
                            node = NextNode();
                            _pos++;
                        }

                        break;
                    } else {
                        buffer.Append(c);
                    }
                }

                if (node == null) {
                    node = FilterTreeNodeValue.Create(buffer.ToString().Trim(), _params, out s);
                }
            }

            if (!_properties.Contains(s)) {
                _properties.Add(s);
            }

            return node;
        }
    }
}
