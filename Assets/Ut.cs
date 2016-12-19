using System;
using System.Collections.Generic;
using System.Text;

using Rnd = UnityEngine.Random;

namespace SimonScreams
{
    static class Ut
    {
        /// <summary>
        ///     Similar to <see cref="string.Substring(int,int)"/>, only for arrays. Returns a new array containing <paramref
        ///     name="length"/> items from the specified <paramref name="startIndex"/> onwards.</summary>
        /// <remarks>
        ///     Returns a new copy of the array even if <paramref name="startIndex"/> is 0 and <paramref name="length"/> is
        ///     the length of the input array.</remarks>
        public static T[] Subarray<T>(this T[] array, int startIndex, int length)
        {
            if (array == null)
                throw new ArgumentNullException("array");
            if (startIndex < 0)
                throw new ArgumentOutOfRangeException("startIndex", "startIndex cannot be negative.");
            if (length < 0 || startIndex + length > array.Length)
                throw new ArgumentOutOfRangeException("length", "length cannot be negative or extend beyond the end of the array.");
            if (startIndex == 0 && length == array.Length)
                return array;
            T[] result = new T[length];
            Array.Copy(array, startIndex, result, 0, length);
            return result;
        }
        
        public static T[] Shuffle<T>(this T[] array)
        {
            if (array == null)
                throw new ArgumentNullException("array");
            for (int j = array.Length; j >= 1; j--)
            {
                int item = Rnd.Range(0, j);
                if (item < j - 1)
                {
                    var t = array[item];
                    array[item] = array[j - 1];
                    array[j - 1] = t;
                }
            }
            return array;
        }

        /// <summary>
        ///     Turns all elements in the enumerable to strings and joins them using the specified <paramref
        ///     name="separator"/> and the specified <paramref name="prefix"/> and <paramref name="suffix"/> for each string.</summary>
        /// <param name="values">
        ///     The sequence of elements to join into a string.</param>
        /// <param name="separator">
        ///     Optionally, a separator to insert between each element and the next.</param>
        /// <param name="prefix">
        ///     Optionally, a string to insert in front of each element.</param>
        /// <param name="suffix">
        ///     Optionally, a string to insert after each element.</param>
        /// <param name="lastSeparator">
        ///     Optionally, a separator to use between the second-to-last and the last element.</param>
        /// <example>
        ///     <code>
        ///         // Returns "[Paris], [London], [Tokyo]"
        ///         (new[] { "Paris", "London", "Tokyo" }).JoinString(", ", "[", "]")
        ///         
        ///         // Returns "[Paris], [London] and [Tokyo]"
        ///         (new[] { "Paris", "London", "Tokyo" }).JoinString(", ", "[", "]", " and ");</code></example>
        public static string JoinString<T>(this IEnumerable<T> values, string separator = null, string prefix = null, string suffix = null, string lastSeparator = null)
        {
            if (values == null)
                throw new ArgumentNullException("values");
            if (lastSeparator == null)
                lastSeparator = separator;

            using (var enumerator = values.GetEnumerator())
            {
                if (!enumerator.MoveNext())
                    return "";

                // Optimise the case where there is only one element
                var one = enumerator.Current;
                if (!enumerator.MoveNext())
                    return prefix + one + suffix;

                // Optimise the case where there are only two elements
                var two = enumerator.Current;
                if (!enumerator.MoveNext())
                {
                    // Optimise the (common) case where there is no prefix/suffix; this prevents an array allocation when calling string.Concat()
                    if (prefix == null && suffix == null)
                        return one + lastSeparator + two;
                    return prefix + one + suffix + lastSeparator + prefix + two + suffix;
                }

                StringBuilder sb = new StringBuilder()
                    .Append(prefix).Append(one).Append(suffix).Append(separator)
                    .Append(prefix).Append(two).Append(suffix);
                var prev = enumerator.Current;
                while (enumerator.MoveNext())
                {
                    sb.Append(separator).Append(prefix).Append(prev).Append(suffix);
                    prev = enumerator.Current;
                }
                sb.Append(lastSeparator).Append(prefix).Append(prev).Append(suffix);
                return sb.ToString();
            }
        }

        /// <summary>
        ///     Returns the index of the first element in this <paramref name="source"/> that is equal to the specified
        ///     <paramref name="element"/> as determined by the specified <paramref name="comparer"/>. If no such elements are
        ///     found, returns <c>-1</c>.</summary>
        public static int IndexOf<T>(this IEnumerable<T> source, T element, IEqualityComparer<T> comparer = null)
        {
            if (source == null)
                throw new ArgumentNullException("source");
            if (comparer == null)
                comparer = EqualityComparer<T>.Default;
            int index = 0;
            foreach (var v in source)
            {
                if (comparer.Equals(v, element))
                    return index;
                index++;
            }
            return -1;
        }
    }
}
