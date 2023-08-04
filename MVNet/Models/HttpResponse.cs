using System.Net;

namespace MVNet
{
    /// <summary>
    /// An HTTP response obtained with a <see cref="HttpClient"/>.
    /// </summary>
    public class HttpResponse : IDisposable
    {
        /// <summary>
        /// The request that retrieved this response.
        /// </summary>
        public HttpRequest Request { get; set; }

        /// <summary>
        /// The HTTP version.
        /// </summary>
        public Version Version { get; set; } = new(1, 1);

        /// <summary>
        /// The status code of the response.
        /// </summary>
        public HttpStatusCode StatusCode { get; set; }

        /// <summary>
        /// The headers of the response.
        /// </summary>
        public Dictionary<string, string> Headers { get; set; } = new(StringComparer.InvariantCultureIgnoreCase);

        /// <summary>
        /// The content of the response.
        /// </summary>
        public HttpContent Content { get; set; }

        /// <summary>
        /// Returns a value indicating whether the request was successful (response code = 200 OK).
        /// </summary>
        public bool IsOK => StatusCode == HttpStatusCode.OK;

        public string Location => this["Location"];

        /// <summary>
        /// Gets a value indicating whether there is a redirect.
        /// </summary>
        public bool HasRedirect
        {
            get
            {
                int numStatusCode = (int)StatusCode;
                return ((int)StatusCode) / 100 == 3
                    || Headers.ContainsKey("Location")
                    || Headers.ContainsKey("Redirect-Location");
            }
        }

        /// <summary>
        /// Returns the HTTP header value.
        /// </summary>
        /// <param name="headerName">The name of the HTTP header.</param>
        /// <value>The value of the HTTP header, if one is set, otherwise the empty string.</value>
        /// <exception cref="System.ArgumentNullException">Parameter value <paramref name="headerName"/> equals <see langword="null"/>.</exception>
        /// <exception cref="System.ArgumentNullException">Parameter value <paramref name="headerName"/> is an empty string.</exception>
        public string this[string headerName]
        {
            get
            {
                #region Parameter check

                if (string.IsNullOrEmpty(headerName))
                    throw new ArgumentNullException(nameof(headerName));

                #endregion

                if (!Headers.TryGetValue(headerName, out string? value))
                    value = string.Empty;

                return value;
            }
        }

        /// <summary>
        /// Determines if the specified cookie is contained at the specified web address.
        /// </summary>
        /// <param name="name">The name of the cookie.</param>
        /// <returns>Value <see langword="true"/>, if the specified cookies are present, otherwise <see langword="false"/>.</returns>
        public bool ContainsCookie(string name)
        {
            return Request.Cookies.Contains(Request.Uri, name);
        }

        public CookieCollection GetCookies()
        {
            return Request.Cookies.GetCookies(Request.Uri);
        }


        /// <inheritdoc/>
        public void Dispose() => Content?.Dispose();
    }
}
