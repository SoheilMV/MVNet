using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading.Tasks;

namespace MVNet
{
    [Serializable]
    public class CookieStorage
    {
        /// <summary>
        /// Original cookie container <see cref="CookieContainer"/> from .NET Framework.
        /// </summary>
        public CookieContainer Container { get; private set; }

        /// <summary>
        /// Number <see cref="Cookie"/> in <see cref="CookieContainer"/> (for all addresses).
        /// </summary>
        public int Count => Container.Count;

        /// <summary>
        /// Gets or sets a value indicating whether the cookie is closed for editing via server responses.
        /// </summary>
        /// <value>The default value is — <see langword="false"/>.</value>
        public bool IsLocked { get; set; }

        /// <summary>
        /// Default value for all instances.
        /// Reset old cookie on call <see cref="Set(Cookie)"/> if a match is found for the domain and cookie name.
        /// </summary>
        public static bool DefaultExpireBeforeSet { get; set; } = true;

        /// <summary>        
        /// Reset old cookie on call <see cref="Set(Cookie)"/> if a match is found for the domain and cookie name.
        /// </summary>
        public bool ExpireBeforeSet { get; set; } = DefaultExpireBeforeSet;

        /// <summary>
        /// Gets or sets the character escaping of the Cookie value received from the server.
        /// </summary>
        public bool EscapeValuesOnReceive { get; set; } = true;

        /// <summary>
        /// Dont throw exception when received cookie name is invalid, just ignore.
        /// </summary>
        public bool IgnoreInvalidCookie { get; set; }

        /// <summary>
        /// Ignore cookies that have expired in response. If you specify <see langword="true" /> (default), an expired Cookie value will not be updated or deleted.
        /// </summary>
        public bool IgnoreSetForExpiredCookies { get; set; } = true;

        /// <summary>
        /// Gets or sets the ability to de-escap the characters in the Cookie value before sending a request to the server.
        /// <remarks>
        /// The default is set to the same value as <see cref="EscapeValuesOnReceive"/>.
        /// In other words, the default mode of operation is as follows: received - screened the value in the storage, send - de-screened the value and sent the original value to the server.
        /// </remarks>
        /// </summary>
        public bool UnescapeValuesOnSend
        {
            get => !_unescapeValuesOnSendCustomized ? EscapeValuesOnReceive : _unescapeValuesOnSend;
            set
            {
                _unescapeValuesOnSendCustomized = true;
                _unescapeValuesOnSend = value;
            }
        }

        private bool _unescapeValuesOnSend;
        private bool _unescapeValuesOnSendCustomized;

        private static readonly char[] ReservedChars = { ' ', '\t', '\r', '\n', '=', ';', ',' };

        private static BinaryFormatter Bf => _binaryFormatter ?? (_binaryFormatter = new BinaryFormatter());
        private static BinaryFormatter _binaryFormatter;


        public CookieStorage(bool isLocked = false, CookieContainer container = null, bool ignoreInvalidCookie = false)
        {
            IsLocked = isLocked;
            Container = container ?? new CookieContainer();
            IgnoreInvalidCookie = ignoreInvalidCookie;
        }

        /// <summary>
        /// Adds a cookie to storage <see cref="CookieContainer"/>.
        /// </summary>
        /// <param name="cookie">Cookie</param>
        public void Add(Cookie cookie)
        {
            Container.Add(cookie);
        }

        /// <summary>
        /// Adds a collection of Cookies to the store <see cref="CookieContainer"/>.
        /// </summary>
        /// <param name="cookies">Cookie Collection</param>
        public void Add(CookieCollection cookies)
        {
            Container.Add(cookies);
        }

        /// <summary>
        /// Adds or updates an existing cookie in storage <see cref="CookieContainer"/>.
        /// </summary>
        /// <param name="cookie">Cookie</param>
        public void Set(Cookie cookie)
        {
            cookie.Name = cookie.Name.Trim();
            cookie.Value = cookie.Value.Trim();

            if (ExpireBeforeSet)
                ExpireIfExists(cookie);

            Add(cookie);
        }

        /// <summary>
        /// Adds or updates existing Cookies from the collection to the store <see cref="CookieContainer"/>.
        /// </summary>
        /// <param name="cookies">Cookie Collection</param>
        public void Set(CookieCollection cookies)
        {
            if (ExpireBeforeSet)
            {
                foreach (Cookie cookie in cookies)
                    ExpireIfExists(cookie);
            }

            Add(cookies);
        }

        /// <inheritdoc cref="Set(System.Net.CookieCollection)"/>
        /// <param name="name">Cookie name</param>
        /// <param name="value">Cookie value</param>
        /// <param name="domain">Domain (no protocol)</param>
        /// <param name="path">Path</param>
        public void Set(string name, string value, string domain, string path = "/")
        {
            var cookie = new Cookie(name, value, path, domain);
            Set(cookie);
        }

        /// <inheritdoc cref="Set(System.Net.CookieCollection)"/>
        /// <param name="requestAddress">Request address</param>
        /// <param name="rawCookie">Raw entry format as a string</param>
        public void Set(Uri requestAddress, string rawCookie)
        {
            // Separate the Cross-domain cookie - if not, there will be an exception.
            // Separate all key=value
            var arguments = rawCookie.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            if (arguments.Length == 0)
                return;

            // Get the key and value of the Cookie itself
            var keyValue = arguments[0].Split(new[] { '=' }, 2);
            if (keyValue.Length <= 1)
                return;

            keyValue[0] = keyValue[0].Trim();
            keyValue[1] = keyValue[1].Trim();

            if (IgnoreInvalidCookie && (string.IsNullOrEmpty(keyValue[0]) || keyValue[0][0] == '$' || keyValue[0].IndexOfAny(ReservedChars) != -1)) return;

            var cookie = new Cookie(keyValue[0], keyValue.Length < 2 ? string.Empty
                : EscapeValuesOnReceive ? Uri.EscapeDataString(keyValue[1]) : keyValue[1]
            );

            bool hasDomainKey = false;

            // Handling Additional Cookie Keys
            for (int i = 1; i < arguments.Length; i++)
            {
                var cookieArgsKeyValues = arguments[i].Split(new[] { '=' }, 2);

                // Handling keys case-insensitively
                string key = cookieArgsKeyValues[0].Trim().ToLower();
                string value = cookieArgsKeyValues.Length < 2 ? null : cookieArgsKeyValues[1].Trim();

                switch (key)
                {
                    case "expires":
                        if (!DateTime.TryParse(value, out var expires) || expires.Year >= 9999)
                            expires = new DateTime(9998, 12, 31, 23, 59, 59, DateTimeKind.Local);

                        cookie.Expires = expires;
                        break;

                    case "path":
                        cookie.Path = value;
                        break;
                    case "domain":
                        string domain = CookieFilters.FilterDomain(value);
                        if (domain == null)
                            continue;

                        hasDomainKey = true;
                        cookie.Domain = domain;
                        break;
                    case "secure":
                        cookie.Secure = true;
                        break;
                    case "httponly":
                        cookie.HttpOnly = true;
                        break;
                }
            }

            if (!hasDomainKey)
            {
                if (string.IsNullOrEmpty(cookie.Path) || cookie.Path.StartsWith("/"))
                    cookie.Domain = requestAddress.Host;
                else if (cookie.Path.Contains("."))
                {
                    string domain = cookie.Path;
                    cookie.Domain = domain;
                    cookie.Path = null;
                }
            }

            if (IgnoreSetForExpiredCookies && cookie.Expired)
                return;

            Set(cookie);
        }


        /// <inheritdoc cref="Set(System.Net.CookieCollection)"/>
        /// <param name="requestAddress">Request address</param>
        /// <param name="rawCookie">Raw entry format as a string</param>
        public void Set(string requestAddress, string rawCookie)
        {
            Set(new Uri(requestAddress), rawCookie);
        }

        private void ExpireIfExists(Uri uri, string cookieName)
        {
            var cookies = Container.GetCookies(uri);
            foreach (Cookie storageCookie in cookies)
            {
                if (storageCookie.Name == cookieName)
                    storageCookie.Expired = true;
            }
        }

        private void ExpireIfExists(Cookie cookie)
        {
            if (string.IsNullOrEmpty(cookie.Domain))
                return;

            // Fast trim: Domain.Remove is slower and much more slower variation: cookie.Domain.TrimStart('.')
            string domain = cookie.Domain[0] == '.' ? cookie.Domain.Substring(1) : cookie.Domain;
            var uri = new Uri($"{(cookie.Secure ? "https://" : "http://")}{domain}");

            ExpireIfExists(uri, cookie.Name);
        }

        /// <summary>
        /// Clear <see cref="CookieContainer"/>.
        /// </summary>
        public void Clear()
        {
            Container = new CookieContainer();
        }

        /// <summary>
        /// delete everything <see cref="Cookie"/> associated with the URL.
        /// </summary>
        /// <param name="url">Resource URL</param>
        public void Remove(string url)
        {
            Remove(new Uri(url));
        }

        /// <inheritdoc cref="Remove(string)"/>
        /// <param name="uri">Resource URI</param>
        public void Remove(Uri uri)
        {
            var cookies = Container.GetCookies(uri);
            foreach (Cookie cookie in cookies)
                cookie.Expired = true;
        }

        /// <summary>
        /// Delete <see cref="Cookie"/> by name for a specific URL.
        /// </summary>
        /// <param name="url">Resource URL</param>
        /// <param name="name">The name of the cookie to be deleted</param>
        public void Remove(string url, string name)
        {
            Remove(new Uri(url), name);
        }

        /// <inheritdoc cref="Remove(string, string)"/>
        /// <param name="uri">Resource URL</param>
        /// <param name="name">The name of the cookie to be deleted</param>
        public void Remove(Uri uri, string name)
        {
            var cookies = Container.GetCookies(uri);
            foreach (Cookie cookie in cookies)
            {
                if (cookie.Name == name)
                    cookie.Expired = true;
            }
        }

        /// <summary>
        /// Gets Cookies in header format for an HTTP request (<see cref="HttpRequestHeader"/>).
        /// </summary>
        /// <param name="uri">Resource URI</param>
        /// <returns>Returns a string containing all cookies for the address.</returns>
        public string GetCookieHeader(Uri uri)
        {
            string header = Container.GetCookieHeader(uri);
            if (!UnescapeValuesOnSend)
                return header;

            // Unescape cookies values
            var sb = new StringBuilder();
            var cookies = header.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (string cookie in cookies)
            {
                var kv = cookie.Split(new[] { '=' }, 2);
                sb.Append(kv[0].Trim());
                sb.Append('=');
                sb.Append(Uri.UnescapeDataString(kv[1].Trim()));
                sb.Append("; ");
            }

            if (sb.Length > 0)
                sb.Remove(sb.Length - 2, 2);

            return sb.ToString();
        }

        /// <inheritdoc cref="GetCookieHeader(System.Uri)"/>
        /// <param name="url">Resource URL</param>
        public string GetCookieHeader(string url)
        {
            return GetCookieHeader(new Uri(url));
        }

        /// <summary>
        /// Gets a collection of all <see cref="Cookie"/> associated with the address of the resource.
        /// </summary>
        /// <param name="uri">Resource URI</param>
        /// <returns>Will return a collection <see cref="Cookie"/> associated with resource address</returns>
        public CookieCollection GetCookies(Uri uri)
        {
            return Container.GetCookies(uri);
        }

        /// <inheritdoc cref="GetCookies(System.Uri)"/>
        /// <param name="url">Resource URL</param>
        public CookieCollection GetCookies(string url)
        {
            return GetCookies(new Uri(url));
        }

        /// <summary>
        /// Checks for existence <see cref="Cookie"/> in <see cref="CookieContainer"/> by resource address and cookie key name.
        /// </summary>
        /// <param name="uri">Resource URI</param>
        /// <param name="cookieName">Cookie key name</param>
        /// <returns>Return <see langword="true"/> if the key is found by request.</returns>
        public bool Contains(Uri uri, string cookieName)
        {
            if (Container.Count <= 0)
                return false;

            var cookies = Container.GetCookies(uri);
            return cookies[cookieName] != null;
        }

        /// <inheritdoc cref="Contains(System.Uri, string)"/>
        public bool Contains(string url, string cookieName)
        {
            return Contains(new Uri(url), cookieName);
        }


        #region Load / Save: File

        /// <summary>
        /// Saves cookies to a file.
        /// <remarks>The .jar extension is recommended.</remarks>
        /// </summary>
        /// <param name="filePath">Let to save the file</param>
        /// <param name="overwrite">Overwrite file if it already exists</param>
        public void SaveToFile(string filePath, bool overwrite = true)
        {
            if (!overwrite && File.Exists(filePath))
                throw new ArgumentException(string.Format(Constants.CookieStorage_SaveToFile_FileAlreadyExists, filePath), nameof(filePath));

            using (var fs = new FileStream(filePath, FileMode.OpenOrCreate))
                Bf.Serialize(fs, this);
        }

        /// <summary>
        /// Loading <see cref="CookieStorage"/> from file.
        /// </summary>
        /// <param name="filePath">Path to the cookie file</param>
        /// <returns>Return <see cref="CookieStorage"/>, which is set in the property <see cref="HttpRequest"/> Cookies.</returns>
        public static CookieStorage LoadFromFile(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"Файл с куками '${filePath}' не найден", nameof(filePath));

            using (var fs = new FileStream(filePath, FileMode.Open))
                return (CookieStorage)Bf.Deserialize(fs);
        }

        #endregion

        #region Save / Load: Bytes

        /// <summary>
        /// Stores the cookie in an array of bytes.
        /// </summary>
        public byte[] ToBytes()
        {
            byte[] r;

            using (var ms = new MemoryStream())
            {
                Bf.Serialize(ms, this);
                r = ms.ToArray();
            }

            return r;
        }

        /// <summary>
        /// Loading <see cref="CookieStorage"/> from an array of bytes.
        /// </summary>
        /// <param name="bytes">Массив байт</param>
        /// <returns>Return <see cref="CookieStorage"/>, which is set in the property <see cref="HttpRequest"/> Cookies.</returns>
        public static CookieStorage FromBytes(byte[] bytes)
        {
            using (var ms = new MemoryStream(bytes))
                return (CookieStorage)Bf.Deserialize(ms);
        }

        #endregion
    }
}
