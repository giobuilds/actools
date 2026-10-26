#nullable disable
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using AcTools.Utils.Helpers;

namespace AcTools.DataFile {
    public class IniFileSection : Dictionary<string, string> {
        private readonly IDataReadWrapper _wrapper;

        public IniFileSection(IDataReadWrapper wrapper, IniFileSection original) : base(original) {
            _wrapper = wrapper;
            Commentary = original.Commentary;
            Commentaries = original.Commentaries == null ? null : new Dictionary<string, string>(original.Commentaries);
        }

        public IniFileSection(IDataReadWrapper wrapper) {
            _wrapper = wrapper;
        }

        public IniFileSection Clone() {
            return new IniFileSection(_wrapper, this);
        }

        public string Commentary { get; private set; }

        public Dictionary<string, string> Commentaries { get; private set; }

        public void SetCommentary(string value) {
            Commentary = value;
        }

        public void SetCommentary(string key, string value) {
            if (value == null) {
                RemoveCommentary(key);
                return;
            }

            if (Commentaries == null) Commentaries = new Dictionary<string, string>(1);
            Commentaries[key] = value;
        }

        public void RemoveCommentary(string key) {
            if (Commentaries == null) return;
            Commentaries.Remove(key);
            if (Commentaries.Count == 0) {
                Commentaries = null;
            }
        }

        public new dynamic this[string key] {
            get => ContainsKey(key) ? base[key] : null;
            set {
                try {
                    if (ReferenceEquals(value, IniFile.Nothing)) {
                        Remove(key);
                    } else if (value == null) {
                        Set(key, (string)null);
                    } else {
                        Set(key, value);
                    }
                } catch (Exception) {
                }
            }
        }

        internal void SetDirect(string key, string value) {
            base[key] = value;
        }

        public string GetPossiblyEmpty(string key) {
            return ContainsKey(key) ? base[key] : null;
        }

        public string GetNonEmpty(string key) {
            if (!ContainsKey(key)) return null;
            var value = base[key];
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }

        public string GetNonEmpty(string key, string defaultValue) {
            return GetNonEmpty(key) ?? defaultValue;
        }

        public bool GetBool(string key, bool defaultValue) {
            if (!ContainsKey(key)) return defaultValue;
            var value = GetPossiblyEmpty(key);
            return value == "1";
        }

        public bool? GetBoolNullable(string key) {
            if (!ContainsKey(key)) return null;
            var value = GetPossiblyEmpty(key);
            return value == "1" || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase) || string.Equals(value, "y", StringComparison.OrdinalIgnoreCase);
        }

        public string[] GetStrings(string key, char delimiter = ',') {
            return GetPossiblyEmpty(key)?.Split(delimiter).Select(x => x.Trim()).Where(x => x.Length > 0).ToArray() ?? new string[0];
        }

        private double[] GetVector(string key, int size) {
            var result = new double[size];
            var line = GetNonEmpty(key);
            if (line == null) return result;

            var s = line.Split(',');
            if (s.Length != size) return result;

            for (var i = 0; i < result.Length; i++) {
                FlexibleParser.TryParseDouble(s[i], out result[i]);
            }

            return result;
        }

        private float[] GetVectorF(string key, int size) {
            var result = new float[size];
            var line = GetNonEmpty(key);
            if (line == null) return result;

            var s = line.Split(',');
            if (s.Length != size) return result;

            for (var i = 0; i < result.Length; i++) {
                if (FlexibleParser.TryParseDouble(s[i], out double f)) {
                    result[i] = (float)f;
                }
            }

            return result;
        }

        public double[] GetVector2(string key) {
            return GetVector(key, 2);
        }

        public float[] GetVector2F(string key) {
            return GetVectorF(key, 2);
        }

        public double[] GetVector3(string key) {
            return GetVector(key, 3);
        }

        public float[] GetVector3F(string key) {
            return GetVectorF(key, 3);
        }

        public double[] GetVector4(string key) {
            return GetVector(key, 4);
        }

        public float[] GetVector4F(string key) {
            return GetVectorF(key, 4);
        }

        public double? GetDoubleNullable(string key) {
            return FlexibleParser.TryParseDouble(GetPossiblyEmpty(key));
        }

        public double GetDouble(string key, double defaultValue) {
            return FlexibleParser.ParseDouble(GetPossiblyEmpty(key), defaultValue);
        }

        public float? GetFloatNullable(string key) {
            return (float?)FlexibleParser.TryParseDouble(GetPossiblyEmpty(key));
        }

        public float GetFloat(string key, float defaultValue) {
            return (float)FlexibleParser.ParseDouble(GetPossiblyEmpty(key), defaultValue);
        }

        public int? GetIntNullable(string key) {
            return FlexibleParser.TryParseInt(GetPossiblyEmpty(key));
        }

        public int GetInt(string key, int defaultValue) {
            return FlexibleParser.TryParseInt(GetPossiblyEmpty(key)) ?? defaultValue;
        }

        public long? GetLongNullable(string key) {
            return FlexibleParser.TryParseLong(GetPossiblyEmpty(key));
        }

        public long GetLong(string key, long defaultValue) {
            return FlexibleParser.TryParseLong(GetPossiblyEmpty(key)) ?? defaultValue;
        }

        /// <summary>
        /// Just in case, this method can parse entries from something like “OVERSTEER_FACTOR” to “OversteerFactor”.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="key"></param>
        /// <param name="ignoreCase"></param>
        /// <returns></returns>
        public T? GetEnumNullable<T>(string key, bool ignoreCase = true) where T : struct, IConvertible {
            if (!typeof(T).IsEnum) {
                throw new ArgumentException("T must be an enumerated type");
            }

            var s = GetNonEmpty(key);
            if (s == null) return null;

            return Enum.TryParse(GetPossiblyEmpty(key), ignoreCase, out T result) ||
                    s.Contains('_') && Enum.TryParse(s.Replace("_", ""), ignoreCase, out result) ? result : (T?)null;
        }

        /// <summary>
        /// Just in case, this method can parse entries from something like “OVERSTEER_FACTOR” to “OversteerFactor”.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="key"></param>
        /// <param name="ignoreCase"></param>
        /// <returns></returns>
        public T GetEnum<T>(string key, T defaultValue, bool ignoreCase = true) where T : struct, IConvertible {
            if (!typeof(T).IsEnum) {
                throw new ArgumentException("T must be an enumerated type");
            }

            var s = GetNonEmpty(key);
            if (s == null) return defaultValue;

            return Enum.TryParse(s, ignoreCase, out T result) ||
                    s.Contains('_') && Enum.TryParse(s.Replace("_", ""), ignoreCase, out result) ? result : defaultValue;
        }

        public T GetIntEnum<T>(string key, T defaultValue) where T : struct, IConvertible {
            if (!typeof(T).IsEnum) {
                throw new ArgumentException("T must be an enumerated type");
            }

            var i = GetInt(key, Convert.ToInt32(defaultValue));
            return Enum.IsDefined(typeof(T), i) ? (T)Enum.ToObject(typeof(T), i) : defaultValue;
        }

        private static void Set(string key, object value) {
            throw new Exception($"Type is not supported: {value?.GetType().ToString() ?? "null"} (key: “{key}”)");
        }

        public void Set(string key, string value) {
            if (value == null) return;
            base[key] = value;
        }

        public void SetOrRemove(string key, string value) {
            if (value != null) {
                base[key] = value;
            } else if (ContainsKey(key)){
                Remove(key);
            }
        }

        public void SetOrRemove(string key, bool value) {
            if (value) {
                base[key] = "1";
            } else if (ContainsKey(key)){
                Remove(key);
            }
        }

        public void Set<T>(string key, IEnumerable<T> value, char delimiter = ',') {
            if (value == null) return;
            Set(key, value.Select(x => x.ToInvariantString()).JoinToString(delimiter));
        }

        public void SetId(string key, string value) {
            if (value == null) return;
            base[key] = value.ToLowerInvariant();
        }

        public void Set(string key, Enum value) {
            base[key] = Convert.ToDouble(value).ToString(CultureInfo.InvariantCulture);
        }

        public void SetIntEnum(string key, Enum value) {
            base[key] = Convert.ToInt32(value).ToString(CultureInfo.InvariantCulture);
        }

        public void Set(string key, int value) {
            base[key] = value.ToString(CultureInfo.InvariantCulture);
        }

        public void Set(string key, int? value) {
            if (!value.HasValue) return;
            base[key] = value.Value.ToString(CultureInfo.InvariantCulture);
        }

        public void Set(string key, long value) {
            base[key] = value.ToString(CultureInfo.InvariantCulture);
        }

        public void Set(string key, long? value) {
            if (!value.HasValue) return;
            base[key] = value.Value.ToString(CultureInfo.InvariantCulture);
        }

        private static string DoubleToString(double d) {
            return d.ToString(CultureInfo.InvariantCulture);
            // return s.IndexOf('.') == -1 ? $"{s}.0" : s;
        }

        public void Set(string key, double value) {
            base[key] = DoubleToString(value);
        }

        public void Set(string key, double? value) {
            if (!value.HasValue) return;
            base[key] = DoubleToString(value.Value);
        }

        public void Set(string key, double value, string format) {
            base[key] = value.ToString(format, CultureInfo.InvariantCulture);
        }

        public void Set(string key, double? value, string format) {
            if (!value.HasValue) return;
            base[key] = value.Value.ToString(format, CultureInfo.InvariantCulture);
        }

        public void Set(string key, bool value) {
            base[key] = value ? "1" : "0";
        }

        public void Set(string key, bool? value) {
            if (!value.HasValue) return;
            base[key] = value.Value ? "1" : "0";
        }
    }
}