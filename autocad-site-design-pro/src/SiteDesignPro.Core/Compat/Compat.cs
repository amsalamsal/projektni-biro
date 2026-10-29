using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;

namespace SiteDesignPro.Core
{
    /// <summary>Small helpers whose BCL equivalents do not exist on .NET Framework 4.7 (AutoCAD 2020-2024).</summary>
    public static class Compat
    {
        public static bool IsFinite(double d) => !double.IsNaN(d) && !double.IsInfinity(d);

        public static byte[] Sha256(byte[] data)
        {
            using (SHA256 sha = SHA256.Create())
            {
                return sha.ComputeHash(data);
            }
        }

        /// <summary>Moves <paramref name="temp"/> over <paramref name="target"/>; File.Replace keeps the swap atomic on NTFS.</summary>
        public static void ReplaceFile(string temp, string target)
        {
#if NETFRAMEWORK
            if (File.Exists(target))
            {
                File.Replace(temp, target, null, true);
            }
            else
            {
                File.Move(temp, target);
            }
#else
            File.Move(temp, target, true);
#endif
        }

        public static int Hash(params object?[] values)
        {
            unchecked
            {
                int h = 17;
                foreach (object? v in values)
                {
                    h = (h * 31) + (v?.GetHashCode() ?? 0);
                }

                return h;
            }
        }
    }

#if NETFRAMEWORK
    /// <summary>String and LINQ overloads added after .NET Framework 4.7.</summary>
    internal static class CompatExtensions
    {
        public static bool Contains(this string s, string value, StringComparison comparison) => s.IndexOf(value, comparison) >= 0;

        public static bool Contains(this string s, char value, StringComparison comparison) => s.IndexOf(value.ToString(), comparison) >= 0;

        public static int IndexOf(this string s, char value, StringComparison comparison) => s.IndexOf(value.ToString(), comparison);

        public static string Replace(this string s, string oldValue, string newValue, StringComparison comparison)
        {
            if (comparison != StringComparison.Ordinal)
            {
                throw new NotSupportedException("Only ordinal replacement is supported.");
            }

            return s.Replace(oldValue, newValue);
        }

        public static IEnumerable<T> Append<T>(this IEnumerable<T> source, T item)
        {
            foreach (T x in source)
            {
                yield return x;
            }

            yield return item;
        }

        public static IEnumerable<T> Prepend<T>(this IEnumerable<T> source, T item)
        {
            yield return item;
            foreach (T x in source)
            {
                yield return x;
            }
        }

        public static HashSet<T> ToHashSet<T>(this IEnumerable<T> source) => new HashSet<T>(source);

        public static HashSet<T> ToHashSet<T>(this IEnumerable<T> source, IEqualityComparer<T> comparer) => new HashSet<T>(source, comparer);

        public static TValue GetValueOrDefault<TKey, TValue>(this IReadOnlyDictionary<TKey, TValue> d, TKey key, TValue fallback) => d.TryGetValue(key, out TValue? v) ? v : fallback;

        public static bool TryAdd<TKey, TValue>(this Dictionary<TKey, TValue> d, TKey key, TValue value)
            where TKey : notnull
        {
            if (d.ContainsKey(key))
            {
                return false;
            }

            d.Add(key, value);
            return true;
        }

        public static bool StartsWith(this string s, char c) => s.Length > 0 && s[0] == c;

        public static bool EndsWith(this string s, char c) => s.Length > 0 && s[s.Length - 1] == c;

        public static string[] Split(this string s, char c, StringSplitOptions options) => s.Split(new[] { c }, options);
    }
#endif
}
