using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MVNet
{
    /// <inheritdoc />
    /// <summary>
    /// An exception indicating that one or more substrings between two substrings could not be found.
    /// </summary>
    public class SubstringException : Exception
    {
        /// <inheritdoc />
        /// <summary>
        /// An exception indicating that one or more substrings between two substrings could not be found.
        /// </summary>
        public SubstringException() { }

        /// <inheritdoc />
        /// <inheritdoc cref="SubstringException()"/>
        public SubstringException(string message) : base(message) { }

        /// <inheritdoc />
        /// <inheritdoc cref="SubstringException()"/>
        public SubstringException(string message, Exception innerException) : base(message, innerException) { }
    }

    /// <summary>
    /// This class is an extension for strings. You don't need to call it directly.
    /// </summary>
    public static class StringExtensions
    {
        #region Substrings: Multiple strings

        /// <summary>
        /// Cuts multiple lines between two substrings. If there are no matches, it will return an empty array.
        /// </summary>
        /// <param name="self">String where to look for substrings</param>
        /// <param name="left">Initial substring</param>
        /// <param name="right">End substring</param>
        /// <param name="startIndex">Search starting from index</param>
        /// <param name="comparison">String comparison method</param>
        /// <param name="limit">Maximum number of substrings to search</param>
        /// <exception cref="ArgumentNullException">Occurs if one of the parameters is an empty string or <keyword>null</keyword>.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Occurs if the start index is greater than the length of the string.</exception>
        /// <returns>Returns an array of substrings that match the pattern, or an empty array if there are no matches.</returns>
        public static string[] SubstringsOrEmpty(this string self, string left, string right, int startIndex = 0, StringComparison comparison = StringComparison.Ordinal, int limit = 0)
        {
            #region Parameter Check

            if (string.IsNullOrEmpty(self))
                return new string[0];

            if (string.IsNullOrEmpty(left))
                throw new ArgumentNullException(nameof(left));

            if (string.IsNullOrEmpty(right))
                throw new ArgumentNullException(nameof(right));

            if (startIndex < 0 || startIndex >= self.Length)
                throw new ArgumentOutOfRangeException(nameof(startIndex));

            #endregion

            int currentStartIndex = startIndex;
            int current = limit;
            var strings = new List<string>();

            while (true)
            {
                if (limit > 0)
                {
                    --current;
                    if (current < 0)
                        break;
                }

                // We are looking for the beginning of the position of the left substring.
                int leftPosBegin = self.IndexOf(left, currentStartIndex, comparison);
                if (leftPosBegin == -1)
                    break;

                // We calculate the end position of the left substring.
                int leftPosEnd = leftPosBegin + left.Length;
                // We are looking for the beginning of the position of the right line.
                int rightPos = self.IndexOf(right, leftPosEnd, comparison);
                if (rightPos == -1)
                    break;

                // We calculate the length of the found substring.
                int length = rightPos - leftPosEnd;
                strings.Add(self.Substring(leftPosEnd, length));
                // We calculate the end position of the right substring.
                currentStartIndex = rightPos + right.Length;
            }

            return strings.ToArray();
        }


        /// <inheritdoc cref="SubstringsOrEmpty"/>
        /// <summary>
        /// Cuts multiple lines between two substrings. If there are no matches, it will return <keyword>null</keyword>.
        /// <remarks>
        /// Created for convenience, for writing exceptions through ?? ternary operator.
        /// </remarks>
        /// <example>
        /// str.Substrings("<tag>","</tag>") ?? throw new Exception("String not found");
        /// </example>
        /// 
        /// <remarks>
        /// Don't forget the function <see cref="SubstringsEx"/> - which throws an exception <see cref="SubstringException"/> if there is no match.
        /// </remarks>
        /// </summary>
        /// <param name="fallback">Value if substrings are not found</param>
        /// <returns>Returns an array of substrings that match the pattern or <keyword>null</keyword>.</returns>
        public static string[] Substrings(this string self, string left, string right, int startIndex = 0, StringComparison comparison = StringComparison.Ordinal, int limit = 0, string[] fallback = null)
        {
            var result = SubstringsOrEmpty(self, left, right, startIndex, comparison, limit);

            return result.Length > 0 ? result : fallback;
        }


        /// <inheritdoc cref="SubstringsOrEmpty"/>
        /// <summary>
        /// Cuts multiple lines between two substrings. If there is no match, an exception will be thrown. <see cref="SubstringException"/>.
        /// </summary>
        /// <exception cref="SubstringException">Will be thrown if no match was found</exception>
        /// <returns>Returns an array of substrings that match the pattern, or throws an exception <see cref="SubstringException"/> if no matches were found.</returns>
        public static string[] SubstringsEx(this string self, string left, string right, int startIndex = 0, StringComparison comparison = StringComparison.Ordinal, int limit = 0)
        {
            var result = SubstringsOrEmpty(self, left, right, startIndex, comparison, limit);
            if (result.Length == 0)
                throw new SubstringException($"Substrings not found. Left: \"{left}\". Right: \"{right}\".");

            return result;
        }

        #endregion


        #region Substring: One substring. Direct order (left to right)

        /// <summary>
        /// Cuts one string between two substrings. If there are no matches, it will return <paramref name="fallback"/> or by default <keyword>null</keyword>.
        /// <remarks>
        /// Created for convenience, for writing exceptions through ?? ternary operator.</remarks>
        /// <example>
        /// str.Between("<tag>","</tag>") ?? throw new Exception("String not found");
        /// </example>
        /// 
        /// <remarks>
        /// Don't forget the function <see cref="SubstringEx"/> - which throws an exception <see cref="SubstringException"/> if there is no match.
        /// </remarks>
        /// </summary>
        /// <param name="self">String where to look for substrings</param>
        /// <param name="left">Initial substring</param>
        /// <param name="right">End substring</param>
        /// <param name="startIndex">Search starting from index</param>
        /// <param name="comparison">String comparison method</param>
        /// <param name="fallback">Value if the substring is not found</param>
        /// <returns>Returns a string between two substrings or <paramref name="fallback"/> (default <keyword>null</keyword>).</returns>
        public static string Substring(this string self, string left, string right, int startIndex = 0, StringComparison comparison = StringComparison.Ordinal, string fallback = null)
        {
            if (string.IsNullOrEmpty(self) || string.IsNullOrEmpty(left) || string.IsNullOrEmpty(right) ||
                startIndex < 0 || startIndex >= self.Length)
                return fallback;

            // We are looking for the beginning of the position of the left substring.
            int leftPosBegin = self.IndexOf(left, startIndex, comparison);
            if (leftPosBegin == -1)
                return fallback;

            // We calculate the end position of the left substring.
            int leftPosEnd = leftPosBegin + left.Length;
            // We are looking for the beginning of the position of the right line.
            int rightPos = self.IndexOf(right, leftPosEnd, comparison);

            return rightPos != -1 ? self.Substring(leftPosEnd, rightPos - leftPosEnd) : fallback;
        }


        /// <inheritdoc cref="Substring"/>
        /// <summary>
        /// Cuts one string between two substrings. If there are no matches, it will return an empty string.
        /// </summary>
        /// <returns>Returns a string between two substrings. If there are no matches, it will return an empty string.</returns>
        public static string SubstringOrEmpty(this string self, string left, string right,
            int startIndex = 0, StringComparison comparison = StringComparison.Ordinal)
        {
            return Substring(self, left, right, startIndex, comparison, string.Empty);
        }

        /// <inheritdoc cref="Substring"/>
        /// <summary>
        /// Cuts one string between two substrings. If there is no match, an exception will be thrown. <see cref="SubstringException"/>.
        /// </summary>
        /// <exception cref="SubstringException">Will be thrown if no match was found</exception>
        /// <returns>Returns a string between two substrings or throws an exception <see cref="SubstringException"/> if no matches were found.</returns>
        public static string SubstringEx(this string self, string left, string right, int startIndex = 0, StringComparison comparison = StringComparison.Ordinal)
        {
            return Substring(self, left, right, startIndex, comparison) ?? throw new SubstringException($"Substring not found. Left: \"{left}\". Right: \"{right}\".");
        }


        #endregion


        #region Cut one substring. Reverse order (right to left)

        /// <inheritdoc cref="Substring"/>
        /// <summary>
        /// Cuts one string between two substrings, only starting at the end. If there are no matches, it will return <paramref name="notFoundValue"/> or by default <keyword>null</keyword>.
        /// <remarks>
        /// Created for convenience, for writing exceptions through ?? ternary operator.</remarks>
        /// <example>
        /// str.BetweenLast("<tag>","</tag>") ?? throw new Exception("String not found");
        /// </example>
        /// 
        /// <remarks>
        /// Don't forget the function <see cref="SubstringLastEx"/> - which throws an exception <see cref="SubstringException"/> if there is no match.
        /// </remarks>
        /// </summary>
        public static string SubstringLast(this string self, string right, string left, int startIndex = -1, StringComparison comparison = StringComparison.Ordinal, string notFoundValue = null)
        {
            if (string.IsNullOrEmpty(self) || string.IsNullOrEmpty(right) || string.IsNullOrEmpty(left) ||
                startIndex < -1 || startIndex >= self.Length)
                return notFoundValue;

            if (startIndex == -1)
                startIndex = self.Length - 1;

            // We are looking for the beginning of the position of the right substring from the end of the string
            int rightPosBegin = self.LastIndexOf(right, startIndex, comparison);
            if (rightPosBegin == -1 || rightPosBegin == 0) // в обратном поиске имеет смысл проверять на 0
                return notFoundValue;

            // Calculate the beginning of the position of the left substring
            int leftPosBegin = self.LastIndexOf(left, rightPosBegin - 1, comparison);
            // If the left end is not found or the right and left substrings are glued together - return an empty string
            if (leftPosBegin == -1 || rightPosBegin - leftPosBegin == 1)
                return notFoundValue;

            int leftPosEnd = leftPosBegin + left.Length;
            return self.Substring(leftPosEnd, rightPosBegin - leftPosEnd);
        }


        /// <inheritdoc cref="SubstringOrEmpty"/>
        /// <summary>
        /// Cuts one string between two substrings, only starting at the end. If there are no matches, it will return an empty string.
        /// </summary>
        public static string SubstringLastOrEmpty(this string self, string right, string left, int startIndex = -1, StringComparison comparison = StringComparison.Ordinal)
        {
            return SubstringLast(self, right, left, startIndex, comparison, string.Empty);
        }

        /// <inheritdoc cref="SubstringEx"/>
        /// <summary>
        /// Cuts one string between two substrings, only starting at the end. If there is no match, an exception will be thrown. <see cref="SubstringException"/>.
        /// </summary>
        public static string SubstringLastEx(this string self, string right, string left, int startIndex = -1, StringComparison comparison = StringComparison.Ordinal)
        {
            return SubstringLast(self, right, left, startIndex, comparison) ?? throw new SubstringException($"StringBetween not found. Right: \"{right}\". Left: \"{left}\".");
        }

        #endregion


        #region Additional functions

        /// <summary>
        /// Checks for the presence of a substring in a string, case insensitive, through comparison: <see cref="StringComparison.OrdinalIgnoreCase" />.
        /// </summary>
        /// <param name="self">Line</param>
        /// <param name="value">The substring to look for in the source string</param>
        /// <returns>Return <langword>true</langword> </returns>
        public static bool ContainsInsensitive(this string self, string value)
        {
            return self.IndexOf(value, StringComparison.OrdinalIgnoreCase) != -1;
        }

        #endregion
    }
}
