// Small shims that let the translated material-color-utilities sources keep
// Java semantics where C# differs (rounding, Optional, collection helpers).
using System;
using System.Collections.Generic;

namespace M3E4Unity.MaterialColor
{
    /// <summary>java.lang.Math with Java's exact semantics.</summary>
    public static class JMath
    {
        public const double PI = Math.PI;
        public const double E = Math.E;

        /// <summary>Java Math.round: floor(x + 0.5). C# Math.Round would use banker's rounding.</summary>
        public static long round(double x)
        {
            if (double.IsNaN(x)) return 0;
            return (long)Math.Floor(x + 0.5);
        }

        public static int round(float x)
        {
            if (float.IsNaN(x)) return 0;
            return (int)Math.Floor(x + 0.5f);
        }

        public static double max(double a, double b) => Math.Max(a, b);
        public static int max(int a, int b) => Math.Max(a, b);
        public static long max(long a, long b) => Math.Max(a, b);
        public static float max(float a, float b) => Math.Max(a, b);
        public static double min(double a, double b) => Math.Min(a, b);
        public static int min(int a, int b) => Math.Min(a, b);
        public static long min(long a, long b) => Math.Min(a, b);
        public static float min(float a, float b) => Math.Min(a, b);
        public static double abs(double a) => Math.Abs(a);
        public static int abs(int a) => Math.Abs(a);
        public static float abs(float a) => Math.Abs(a);
        public static double floor(double a) => Math.Floor(a);
        public static double ceil(double a) => Math.Ceiling(a);
        public static double pow(double a, double b) => Math.Pow(a, b);
        public static double sqrt(double a) => Math.Sqrt(a);
        public static double cbrt(double a) => Math.Cbrt(a);
        public static double atan2(double y, double x) => Math.Atan2(y, x);
        public static double atan(double a) => Math.Atan(a);
        public static double sin(double a) => Math.Sin(a);
        public static double cos(double a) => Math.Cos(a);
        public static double tan(double a) => Math.Tan(a);
        public static double exp(double a) => Math.Exp(a);
        public static double log(double a) => Math.Log(a);
        public static double log10(double a) => Math.Log10(a);
        public static double hypot(double a, double b) => Math.Sqrt(a * a + b * b);

        /// <summary>log(1 + x), accurate for small x.</summary>
        public static double log1p(double x)
        {
            double u = 1.0 + x;
            return u == 1.0 ? x : Math.Log(u) * x / (u - 1.0);
        }

        /// <summary>exp(x) - 1, accurate for small x.</summary>
        public static double expm1(double x)
        {
            double u = Math.Exp(x);
            if (u == 1.0) return x;
            double um1 = u - 1.0;
            if (um1 == -1.0) return -1.0;
            return um1 * x / Math.Log(u);
        }
        public static double toRadians(double deg) => deg / 180.0 * Math.PI;
        public static double toDegrees(double rad) => rad * 180.0 / Math.PI;
        public static double signum(double a) => a > 0 ? 1.0 : a < 0 ? -1.0 : a;
        public static int floorMod(int a, int b) { int m = a % b; return (m != 0 && ((m ^ b) < 0)) ? m + b : m; }
        public static int floorDiv(int a, int b) { int q = a / b; if ((a % b != 0) && ((a ^ b) < 0)) q--; return q; }
    }

    /// <summary>java.util.Optional.</summary>
    public sealed class Optional<T> where T : class
    {
        static readonly Optional<T> Empty = new Optional<T>(null);
        readonly T value;
        Optional(T value) { this.value = value; }
        public static Optional<T> of(T value) => new Optional<T>(value ?? throw new ArgumentNullException(nameof(value)));
        public static Optional<T> ofNullable(T value) => value == null ? Empty : new Optional<T>(value);
        public static Optional<T> empty() => Empty;
        public bool isPresent() => value != null;
        public bool isEmpty() => value == null;
        public T get() => value ?? throw new InvalidOperationException("No value present");
        public T orElse(T other) => value ?? other;
        public T orElseGet(Func<T> other) => value ?? other();
        public Optional<TResult> map<TResult>(Func<T, TResult> f) where TResult : class =>
            value == null ? Optional<TResult>.empty() : Optional<TResult>.ofNullable(f(value));
    }

    public static class Optional
    {
        public static Optional<T> of<T>(T value) where T : class => Optional<T>.of(value);
        public static Optional<T> ofNullable<T>(T value) where T : class => Optional<T>.ofNullable(value);
        public static Optional<T> empty<T>() where T : class => Optional<T>.empty();
    }

    /// <summary>java.util.Random, bit-exact (QuantizerWsmeans seeds it, so results depend on it).</summary>
    public sealed class JavaRandom
    {
        const long Multiplier = 0x5DEECE66DL;
        const long Addend = 0xBL;
        const long Mask = (1L << 48) - 1;
        long seed;

        public JavaRandom(long seed) { this.seed = (seed ^ Multiplier) & Mask; }

        int next(int bits)
        {
            seed = (seed * Multiplier + Addend) & Mask;
            return (int)((ulong)seed >> (48 - bits));
        }

        public int nextInt(int bound)
        {
            if (bound <= 0) throw new ArgumentException("bound must be positive");
            if ((bound & -bound) == bound) return (int)((bound * (long)next(31)) >> 31);
            int bits, val;
            do
            {
                bits = next(31);
                val = bits % bound;
            } while (unchecked(bits - val + (bound - 1)) < 0);
            return val;
        }
    }

    /// <summary>java.util.Collections (stable sort, like Java's TimSort).</summary>
    public static class Collections
    {
        public static List<T> singletonList<T>(T item) => new List<T> { item };
        public static List<T> unmodifiableList<T>(List<T> list) => list;

        public static void sort<T>(List<T> list, IComparer<T> comparer)
        {
            var sorted = Arrays.StableSorted(list, comparer);
            for (int i = 0; i < sorted.Count; i++) list[i] = sorted[i];
        }

        public static void sort<T>(List<T> list, Comparison<T> comparison) => sort(list, Comparer<T>.Create(comparison));
    }

    /// <summary>java.util.Arrays.</summary>
    public static class Arrays
    {
        public static List<T> asList<T>(params T[] items) => new List<T>(items);

        public static void fill<T>(T[] array, T value)
        {
            for (int i = 0; i < array.Length; i++) array[i] = value;
        }

        public static void sort<T>(T[] array) where T : IComparable<T>
        {
            var sorted = StableSorted(array, Comparer<T>.Default);
            for (int i = 0; i < sorted.Count; i++) array[i] = sorted[i];
        }

        internal static List<T> StableSorted<T>(IList<T> items, IComparer<T> comparer)
        {
            // merge sort: stable, like Java's object sort
            var a = new List<T>(items);
            var tmp = new T[a.Count];
            for (int width = 1; width < a.Count; width *= 2)
            {
                for (int lo = 0; lo < a.Count - width; lo += 2 * width)
                {
                    int mid = lo + width, hi = Math.Min(lo + 2 * width, a.Count);
                    int i = lo, j = mid, k = lo;
                    while (i < mid && j < hi) tmp[k++] = comparer.Compare(a[j], a[i]) < 0 ? a[j++] : a[i++];
                    while (i < mid) tmp[k++] = a[i++];
                    while (j < hi) tmp[k++] = a[j++];
                    for (k = lo; k < hi; k++) a[k] = tmp[k];
                }
            }
            return a;
        }
    }

    /// <summary>Java collection / String / enum method names used by the translated sources.</summary>
    public static class JavaExtensions
    {
        public static T get<T>(this List<T> list, int index) => list[index];
        public static T get<T>(this IList<T> list, int index) => list[index];

        /// <summary>Map.get: null (default) when missing.</summary>
        public static TValue get<TKey, TValue>(this Dictionary<TKey, TValue> map, TKey key) =>
            map.TryGetValue(key, out var v) ? v : default;

        public static TValue getOrDefault<TKey, TValue>(this Dictionary<TKey, TValue> map, TKey key, TValue fallback) =>
            map.TryGetValue(key, out var v) ? v : fallback;

        public static bool containsKey<TKey, TValue>(this Dictionary<TKey, TValue> map, TKey key) => map.ContainsKey(key);
        public static HashSet<TKey> keySet<TKey, TValue>(this Dictionary<TKey, TValue> map) => new HashSet<TKey>(map.Keys);
        public static void clear<TKey, TValue>(this Dictionary<TKey, TValue> map) => map.Clear();
        public static void clear<T>(this List<T> list) => list.Clear();
        public static bool contains<T>(this List<T> list, T item) => list.Contains(item);
        public static bool endsWith(this string s, string suffix) => s.EndsWith(suffix, StringComparison.Ordinal);
        public static bool startsWith(this string s, string prefix) => s.StartsWith(prefix, StringComparison.Ordinal);
        public static string toLowerCase(this string s) => s.ToLowerInvariant();
        public static string name(this Enum e) => e.ToString();
        public static int ordinal(this Enum e) => Convert.ToInt32(e);
        public static int compareTo(this ColorSpec.SpecVersion a, ColorSpec.SpecVersion b) => ((int)a).CompareTo((int)b);
    }
}
