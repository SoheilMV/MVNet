namespace MVNet
{
    public static class CookieFilters
    {
        public static bool Enabled { get; set; } = true;

        public static bool Trim { get; set; } = true;
        public static bool Path { get; set; } = true;
        public static bool CommaEndingValue { get; set; } = true;

        /// <summary>
        /// We filter Cookies for further use in native storage.
        /// </summary>
        /// <param name="rawCookie">Cookie entry as a string with all parameters</param>
        /// <returns>Filtered Cookie as a string with all filtered parameters</returns>
        public static string Filter(string rawCookie)
        {
            return !Enabled ? rawCookie : rawCookie
                   .TrimWhitespace()
                   .FilterPath()
                   .FilterInvalidExpireYear()
                   .FilterCommaEndingValue();
        }

        /// <summary>
        /// Filter bad domains before placing <see cref="System.Net.Cookie"/> in <see cref="CookieStorage"/>.
        /// </summary>
        /// <param name="domain">Cookie domain from domain header</param>
        /// <returns>Return <see langword="null"/> if the domain is not valid for storage <see cref="CookieStorage"/></returns>
        public static string FilterDomain(string domain)
        {
            if (string.IsNullOrWhiteSpace(domain))
                return null;

            domain = domain.Trim('\t', '\n', '\r', ' ');
            bool isWildCard = domain.Length > 1 && domain[0] == '.';
            bool isFirstLevel = domain.IndexOf('.', 1) == -1;

            // Local wildcard domains aren't accepted by CookieStorage and native CookieContainer.
            return isWildCard && isFirstLevel ? domain.Substring(1) : domain;
        }

        /// <summary>
        /// Remove any spaces at the beginning and end
        /// </summary>
        private static string TrimWhitespace(this string rawCookie)
        {
            return !Trim ? rawCookie : rawCookie.Trim();
        }

        /// <summary>
        /// Replace all path values with "/"
        /// </summary>
        private static string FilterPath(this string rawCookie)
        {
            if (!Path)
                return rawCookie;

            const string path = "path=/";
            int pathIndex = rawCookie.IndexOf(path, 0, StringComparison.OrdinalIgnoreCase);
            if (pathIndex == -1)
                return rawCookie;

            pathIndex += path.Length;
            if (pathIndex >= rawCookie.Length - 1 || rawCookie[pathIndex] == ';')
                return rawCookie;

            int endPathIndex = rawCookie.IndexOf(';', pathIndex);
            if (endPathIndex == -1)
                endPathIndex = rawCookie.Length;

            return rawCookie.Remove(pathIndex, endPathIndex - pathIndex);
        }


        /// <summary>
        /// Replacing cookie values ending with a comma (escape)
        /// </summary>
        private static string FilterCommaEndingValue(this string rawCookie)
        {
            if (!CommaEndingValue)
                return rawCookie;

            int equalIndex = rawCookie.IndexOf('=');
            if (equalIndex == -1 || equalIndex >= rawCookie.Length - 1)
                return rawCookie;

            int endValueIndex = rawCookie.IndexOf(';', equalIndex + 1);
            if (endValueIndex == -1)
                endValueIndex = rawCookie.Length - 1;

            int lastCharIndex = endValueIndex - 1;
            return rawCookie[lastCharIndex] != ','
                ? rawCookie
                : rawCookie.Remove(lastCharIndex, 1).Insert(lastCharIndex, "%2C");
        }

        /// <summary>
        /// Fixes an exception at GMT 9999 by replacing it with 9998.
        /// </summary>
        /// <returns>Will return a corrected cookie with the year 9998 instead of 9999 which may throw an exception.</returns>
        private static string FilterInvalidExpireYear(this string rawCookie)
        {
            const string expireKey = "expires=";
            const string invalidYear = "9999";

            int startIndex = rawCookie.IndexOf(expireKey, StringComparison.OrdinalIgnoreCase);
            if (startIndex == -1)
                return rawCookie;
            startIndex += expireKey.Length;

            int endIndex = rawCookie.IndexOf(';', startIndex);
            if (endIndex == -1)
                endIndex = rawCookie.Length;

            string expired = rawCookie.Substring(startIndex, endIndex - startIndex);

            int invalidYearIndex = expired.IndexOf(invalidYear, StringComparison.Ordinal);
            if (invalidYearIndex == -1)
                return rawCookie;
            invalidYearIndex += startIndex + invalidYear.Length - 1;

            return rawCookie.Remove(invalidYearIndex, 1).Insert(invalidYearIndex, "8");
        }
    }
}
