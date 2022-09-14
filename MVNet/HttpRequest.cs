using System.Net.Security;
using System.Text;
using System.Security.Authentication;
using System.Net;
using System.Net.Sockets;
using System.Globalization;
using System.Security;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;

namespace MVNet
{
    /// <summary>
    /// Class to send HTTP-server requests.
    /// </summary>
    public sealed class HttpRequest : IDisposable
    {
        //Used to determine how many bytes have been sent/read.
        private sealed class HttpWrapperStream : Stream
        {
            #region Fields (private)

            private readonly Stream _baseStream;
            private readonly int _sendBufferSize;

            #endregion

            #region Properties (public)

            public Action<int> BytesReadCallback { private get; set; }

            public Action<int> BytesWriteCallback { private get; set; }

            #region Overridden

            public override bool CanRead => _baseStream.CanRead;

            public override bool CanSeek => _baseStream.CanSeek;

            public override bool CanTimeout => _baseStream.CanTimeout;

            public override bool CanWrite => _baseStream.CanWrite;

            public override long Length => _baseStream.Length;

            public override long Position
            {
                get => _baseStream.Position;
                set => _baseStream.Position = value;
            }

            #endregion

            #endregion

            public HttpWrapperStream(Stream baseStream, int sendBufferSize)
            {
                _baseStream = baseStream;
                _sendBufferSize = sendBufferSize;
            }

            #region Methods (public)

            public override void Flush() { }

            public override void SetLength(long value) => _baseStream.SetLength(value);

            public override long Seek(long offset, SeekOrigin origin)
            {
                return _baseStream.Seek(offset, origin);
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                int bytesRead = _baseStream.Read(buffer, offset, count);

                BytesReadCallback?.Invoke(bytesRead);

                return bytesRead;
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
                if (BytesWriteCallback == null)
                    _baseStream.Write(buffer, offset, count);
                else
                {
                    int index = 0;

                    while (count > 0)
                    {
                        int bytesWrite;

                        if (count >= _sendBufferSize)
                        {
                            bytesWrite = _sendBufferSize;
                            _baseStream.Write(buffer, index, bytesWrite);

                            index += _sendBufferSize;
                            count -= _sendBufferSize;
                        }
                        else
                        {
                            bytesWrite = count;
                            _baseStream.Write(buffer, index, bytesWrite);

                            count = 0;
                        }

                        BytesWriteCallback(bytesWrite);
                    }
                }
            }

            #endregion
        }

        #region Static properties (public)

        /// <summary>
        /// Version HTTP-protocol, used in requests.
        /// </summary>
        public static Version ProtocolVersion { get; set; } = new Version(1, 1);

        /// <summary>
        /// Gets or sets a value indicating whether to disable client proxy for local addresses.
        /// </summary>
        /// <value>Default value — <see langword="false"/>.</value>
        public static bool DisableProxyForLocalAddress { get; set; }

        /// <summary>
        /// Gets or sets the global proxy client.
        /// </summary>
        /// <value>Default value — <see langword="null"/>.</value>
        public static ProxyClient GlobalProxy { get; set; }

        #endregion

        #region Fields (private)

        private ProxyClient _currentProxy;

        private int _redirectionCount;
        private int _maximumAutomaticRedirections = 5;

        private int _connectTimeout = 9 * 1000; // 9 Seconds
        private int _readWriteTimeout = 30 * 1000; // 30 Seconds

        private DateTime _whenConnectionIdle;
        private int _keepAliveTimeout = 30 * 1000;
        private int _maximumKeepAliveRequests = 100;
        private int _keepAliveRequestCount;
        private bool _keepAliveReconnected;

        private int _reconnectLimit = 3;
        private int _reconnectDelay = 100;
        private int _reconnectCount;

        private HttpMethod _method;
        private HttpContent _content; // Request body.

        private readonly Dictionary<string, string> _permanentHeaders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // Temporary data that is set through special methods.
        // Deleted after the first request.
        private Dictionary<string, string> _temporaryHeaders;
        private MultipartContent _temporaryMultipartContent;

        // The number of bytes sent and received.
        // Used for the UploadProgressChanged and DownloadProgressChanged events.
        private long _bytesSent;
        private long _totalBytesSent;
        private long _bytesReceived;
        private long _totalBytesReceived;
        private bool _canReportBytesReceived;

        private EventHandler<UploadProgressChangedEventArgs> _uploadProgressChangedHandler;
        private EventHandler<DownloadProgressChangedEventArgs> _downloadProgressChangedHandler;

        // Variables for storing initial properties for the ManualMode switch (manual mode)
        private bool _tempAllowAutoRedirect;
        private bool _tempIgnoreProtocolErrors;

        #endregion

        #region Events (public)

        /// <summary>
        /// Occurs each time the progress of unloading message body data is progressing.
        /// </summary>
        public event EventHandler<UploadProgressChangedEventArgs> UploadProgressChanged
        {
            add => _uploadProgressChangedHandler += value;
            remove => _uploadProgressChangedHandler -= value;
        }

        /// <summary>
        /// Occurs each time the loading of message body data is progressing.
        /// </summary>
        public event EventHandler<DownloadProgressChangedEventArgs> DownloadProgressChanged
        {
            add => _downloadProgressChangedHandler += value;
            remove => _downloadProgressChangedHandler -= value;
        }

        #endregion

        #region Properties (public)

        /// <summary>
        /// Gets or sets the URI of the Internet resource that is used when a relative address is specified in the request.
        /// </summary>
        /// <value>Default value — <see langword="null"/>.</value>
        public Uri BaseAddress { get; set; }

        /// <summary>
        /// Returns the URI of the Internet resource that actually responds to the request.
        /// </summary>
        public Uri Address { get; private set; }

        /// <summary>
        /// Gets or sets the proxy client.
        /// </summary>
        /// <value>Default value — <see langword="null"/>.</value>
        public ProxyClient Proxy { get; set; }

        /// <summary>
        /// A collection of certificates to be considered for the client's authentication to the server.
        /// </summary>
        public X509CertificateCollection ClientCertificates { get; set; }

        /// <summary>
        /// Gets or sets the possible SSL protocols.
        /// The default is: <value>SslProtocols.Tls | SslProtocols.Tls11 | SslProtocols.Tls12 | SslProtocols.Tls13</value>.
        /// </summary>
        public SslProtocols SslProtocols { get; set; }

        /// <summary>
        /// Gets or sets the delegate method called when validating the SSL certificate used for authentication.
        /// </summary>
        /// <value>The default value is — <see langword="null"/>. If set to default, then the method that accepts all SSL certificates is used.</value>
        public RemoteCertificateValidationCallback SslCertificateValidatorCallback { get; set; }

        /// <summary>
        /// Selects the local SSL certificate used for authentication.
        /// </summary>
        /// <value>
        /// The default value is — <see langword="null"/>.
        /// </value>
        public LocalCertificateSelectionCallback? LocalCertificateSelectionCallback { get; set; }

        /// <summary>
        /// Allows empty headers to be set.
        /// </summary>
        public bool AllowEmptyHeaderValues { get; set; }

        /// <summary>
        /// Whether to send temporary headers (added via <see cref="AddHeader(string,string)"/>) forwarded requests.
        /// Default <see langword="true"/>.
        /// </summary>
        public bool KeepTemporaryHeadersOnRedirect { get; set; }

        /// <summary>
        /// Enable tracking headers in intermediate requests (forwarded) and store them in <see cref="HttpResponse.MiddleHeaders"/>.
        /// </summary>
        public bool EnableMiddleHeaders { get; set; }

        /// <summary>
        /// AcceptEncoding header. Note that not all sites accept the version with a space: "gzip, deflate".
        /// </summary>
        public string AcceptEncoding { get; set; }

        /// <summary>
        /// Dont throw exception when received cookie name is invalid, just ignore.
        /// </summary>
        public bool IgnoreInvalidCookie { get; set; }

        #region Behavior

        /// <summary>
        /// Gets or sets a value indicating whether the request should follow redirect responses.
        /// </summary>
        /// <value>Default value — <see langword="true"/>.</value>
        public bool AllowAutoRedirect { get; set; }

        /// <summary>
        /// Switches work with requests to manual mode. If set to false, it will return the original values of the AllowAutoRedirect and IgnoreProtocolErrors fields.
        /// 1. Checking returned HTTP codes is disabled, there will be no exception if the code is different from 200 OK.
        /// 2. Automatic forwarding is disabled.
        /// </summary>
        public bool ManualMode
        {
            get => !AllowAutoRedirect && IgnoreProtocolErrors;
            set
            {
                if (value)
                {
                    _tempAllowAutoRedirect = AllowAutoRedirect;
                    _tempIgnoreProtocolErrors = IgnoreProtocolErrors;

                    AllowAutoRedirect = false;
                    IgnoreProtocolErrors = true;
                }
                else
                {
                    AllowAutoRedirect = _tempAllowAutoRedirect;
                    IgnoreProtocolErrors = _tempIgnoreProtocolErrors;
                }
            }
        }

        /// <summary>
        /// Gets or sets the maximum number of consecutive redirects.
        /// </summary>
        /// <value>Default value - 5.</value>
        /// <exception cref="System.ArgumentOutOfRangeException">The parameter value is less than 1.</exception>
        public int MaximumAutomaticRedirections
        {
            get => _maximumAutomaticRedirections;
            set
            {
                #region Parameter check

                if (value < 1)
                    throw ExceptionHelper.CanNotBeLess(nameof(MaximumAutomaticRedirections), 1);

                #endregion

                _maximumAutomaticRedirections = value;
            }
        }

        /// <summary>
        /// Gets or sets the Cookie header generation option.
        /// If value is specified <value>true</value> - only one Cookie header will be generated, and all Cookies separated by a separator are registered in it.
        /// If value is specified <value>false</value> - each Cookie will be in a new header (new format).
        /// </summary>
        public bool CookieSingleHeader { get; set; } = true;

        /// <summary>
        /// Gets or sets the timeout in milliseconds when connecting to an HTTP server.
        /// </summary>
        /// <value>The default value is 9000ms, which is 9 seconds.</value>
        /// <exception cref="System.ArgumentOutOfRangeException">The parameter value is less than 0.</exception>
        public int ConnectTimeout
        {
            get => _connectTimeout;
            set
            {
                #region Parameter check

                if (value < 0)
                    throw ExceptionHelper.CanNotBeLess(nameof(ConnectTimeout), 0);

                #endregion

                _connectTimeout = value;
            }
        }

        /// <summary>
        /// Gets or sets the timeout, in milliseconds, when writing to or reading from the stream.
        /// </summary>
        /// <value>The default value is 30,000ms, which is 30 seconds.</value>
        /// <exception cref="System.ArgumentOutOfRangeException">The parameter value is less than 0.</exception>
        public int ReadWriteTimeout
        {
            get => _readWriteTimeout;
            set
            {
                #region Parameter check

                if (value < 0)
                    throw ExceptionHelper.CanNotBeLess(nameof(ReadWriteTimeout), 0);

                #endregion

                _readWriteTimeout = value;
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether to ignore protocol errors and not throw exceptions.
        /// </summary>
        /// <value>Default value — <see langword="false"/>.</value>
        /// <remarks>If you set the value <see langword="true"/>, then in case of receiving an erroneous response with a status code of 4xx or 5xx, no exception will be thrown. You can find out the status code of the response using the property <see cref="HttpResponse.StatusCode"/>.</remarks>
        public bool IgnoreProtocolErrors { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether a persistent connection to the Internet resource should be established.
        /// </summary>
        /// <value>Default value - <see langword="true"/>.</value>
        /// <remarks>If the value is <see langword="true"/>, then the 'Connection: Keep-Alive' header is additionally sent, otherwise the 'Connection: Close' header is sent. If an HTTP proxy is used for the connection, then instead of the header - 'Connection', the header is set - 'Proxy-Connection'. If the server terminates the persistent connection, <see cref="HttpResponse"/> will try to connect again, but this only works if the connection goes directly to an HTTP server or an HTTP proxy.</remarks>
        public bool KeepAlive { get; set; }

        /// <summary>
        /// Gets or sets the default time in milliseconds for a persistent connection to be idle.
        /// </summary>
        /// <value>The default value is 30.000 which equals 30 seconds.</value>
        /// <exception cref="System.ArgumentOutOfRangeException">The parameter value is less than 0.</exception>
        /// <remarks>If the time is up, a new connection will be created. If the server returns its timeout value <see cref="HttpResponse.KeepAliveTimeout"/>, then it will be used.</remarks>
        public int KeepAliveTimeout
        {
            get => _keepAliveTimeout;
            set
            {
                #region Parameter check

                if (value < 0)
                    throw ExceptionHelper.CanNotBeLess(nameof(KeepAliveTimeout), 0);

                #endregion

                _keepAliveTimeout = value;
            }
        }

        /// <summary>
        /// Gets or sets the default maximum number of requests allowed per connection.
        /// </summary>
        /// <value>The default value is 100.</value>
        /// <exception cref="System.ArgumentOutOfRangeException">The parameter value is less than 1.</exception>
        /// <remarks>If the number of requests exceeds the maximum, a new connection will be created. If the server returns its value of the maximum number of requests <see cref="HttpResponse.MaximumKeepAliveRequests"/>, then it will be used.</remarks>
        public int MaximumKeepAliveRequests
        {
            get => _maximumKeepAliveRequests;
            set
            {
                #region Parameter check

                if (value < 1)
                    throw ExceptionHelper.CanNotBeLess(nameof(MaximumKeepAliveRequests), 1);

                #endregion

                _maximumKeepAliveRequests = value;
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether to retry after n-milliseconds if an error occurs while connecting or sending/downloading data.
        /// </summary>
        /// <value>Default value - <see langword="false"/>.</value>
        public bool Reconnect { get; set; }

        /// <summary>
        /// Gets or sets the maximum number of reconnect attempts.
        /// </summary>
        /// <value>The default value is 3.</value>
        /// <exception cref="System.ArgumentOutOfRangeException">The parameter value is less than 1.</exception>
        public int ReconnectLimit
        {
            get => _reconnectLimit;
            set
            {
                #region Parameter check

                if (value < 1)
                    throw ExceptionHelper.CanNotBeLess(nameof(ReconnectLimit), 1);

                #endregion

                _reconnectLimit = value;
            }
        }

        /// <summary>
        /// Gets or sets the delay, in milliseconds, that occurs before a reconnect is performed.
        /// </summary>
        /// <value>The default value is 100 milliseconds.</value>
        /// <exception cref="System.ArgumentOutOfRangeException">The parameter value is less than 0.</exception>
        public int ReconnectDelay
        {
            get => _reconnectDelay;
            set
            {
                #region Parameter check

                if (value < 0)
                    throw ExceptionHelper.CanNotBeLess(nameof(ReconnectDelay), 0);

                #endregion

                _reconnectDelay = value;
            }
        }

        #endregion

        #region HTTP headers

        /// <summary>
        /// The language used by the current request.
        /// </summary>
        /// <value>The default value is <see langword="null"/>.</value>
        /// <remarks>If a language is set, then an additional 'Accept-Language' header is sent with the name of that language.</remarks>
        public CultureInfo Culture { get; set; }

        /// <summary>
        /// Gets or sets the encoding used to convert outgoing and incoming data.
        /// </summary>
        /// <value>Default value — <see langword="null"/>.</value>
        /// <remarks>If an encoding is set, then an additional 'Accept-Charset' header is sent with the name of this encoding, but only if this header is not already set directly. The response encoding is determined automatically, but if it cannot be determined, the value of this property will be used. If the value of this property is not set, then the value will be used. <see cref="System.Text.Encoding.Default"/>.</remarks>
        public Encoding CharacterSet { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the content of the response should be encoded. It is used primarily for data compression.
        /// </summary>
        /// <value>Default value - <see langword="true"/>.</value>
        /// <remarks>If the value is <see langword="true"/>, then the 'Accept-Encoding: gzip, deflate' header is additionally sent.</remarks>
        public bool EnableEncodingContent { get; set; }

        /// <summary>
        /// Gets or sets the username for basic authorization on the HTTP server.
        /// </summary>
        /// <value>Default value — <see langword="null"/>.</value>
        /// <remarks>If the value is set, then the 'Authorization' header is additionally sent.</remarks>
        public string Username { get; set; }

        /// <summary>
        /// Gets or sets the password for basic authorization on the HTTP server.
        /// </summary>
        /// <value>Default value — <see langword="null"/>.</value>
        /// <remarks>If the value is set, then the 'Authorization' header is additionally sent.</remarks>
        public string Password { get; set; }

        /// <summary>
        /// Gets or sets the value of the 'User-Agent' HTTP header.
        /// </summary>
        /// <value>Default value — <see langword="null"/>.</value>
        public string UserAgent
        {
            get => this["User-Agent"];
            set => this["User-Agent"] = value;
        }

        /// <summary>
        /// Gets or sets the value of the 'Referer' HTTP header.
        /// </summary>
        /// <value>Default value — <see langword="null"/>.</value>
        public string Referer
        {
            get => this["Referer"];
            set => this["Referer"] = value;
        }

        /// <summary>
        /// Gets or sets the value of the 'Authorization' HTTP header.
        /// </summary>
        /// <value>Default value — <see langword="null"/>.</value>
        public string Authorization
        {
            get => this["Authorization"];
            set => this["Authorization"] = value;
        }

        /// <summary>
        /// Gets or sets the cookie associated with the request.
        /// Created automatically if property is set <see cref="UseCookies"/> in value <see langword="true"/>.
        /// </summary>
        /// <value>Default value: if <see cref="UseCookies"/> installed in <see langword="true"/>, then the collection will return.
        /// If a <see langword="false"/>, then it will come back <see langword="null"/>.</value>
        /// <remarks>Cookies can be changed by the response from the HTTP server. To prevent this, you need to set the property <see cref="MVNet.CookieStorage.IsLocked"/> equal <see langword="true"/>.</remarks>
        public CookieStorage Cookies { get; set; }

        /// <summary>
        /// Allows you to set automatic creation <see cref="CookieStorage"/> in the Cookies property when a cookie is received from the server.
        /// If you set the value to <see langword="false"/> - cookie headers will not be sent and will not be stored from the response (Set-Cookie header).
        /// </summary>
        /// <value>Значение по умолчанию — <see langword="true"/>.</value>
        public bool UseCookies { get; set; } = true;

        #endregion

        #endregion

        #region Properties (internal)

        internal TcpClient TcpClient { get; private set; }

        internal Stream ClientStream { get; private set; }

        internal NetworkStream ClientNetworkStream { get; private set; }

        #endregion

        #region Indexers (public)

        /// <summary>
        /// Gets or sets the HTTP header value.
        /// </summary>
        /// <param name="headerName">The name of the HTTP header.</param>
        /// <value>The value of the HTTP header, if one is set, otherwise the empty string. If you set the value <see langword="null"/> or an empty string, the HTTP header will be removed from the list.</value>
        /// <exception cref="System.ArgumentNullException">Parameter value <paramref name="headerName"/> equals <see langword="null"/>.</exception>
        /// <exception cref="System.ArgumentException">
        /// Parameter value <paramref name="headerName"/> is an empty string.
        /// -or-
        /// Setting the value of the HTTP header, which must be set using a custom property/method.
        /// </exception>
        /// <remarks>List of HTTP headers that should only be set using special properties/methods:
        /// <list type="table">
        ///     <item>
        ///        <description>Accept-Encoding</description>
        ///     </item>
        ///     <item>
        ///        <description>Content-Length</description>
        ///     </item>
        ///     <item>
        ///         <description>Content-Type</description>
        ///     </item>
        ///     <item>
        ///        <description>Connection</description>
        ///     </item>
        ///     <item>
        ///        <description>Proxy-Connection</description>
        ///     </item>
        ///     <item>
        ///        <description>Host</description>
        ///     </item>
        /// </list>
        /// </remarks>
        public string this[string headerName]
        {
            get
            {
                #region Parameter check

                if (headerName == null)
                    throw new ArgumentNullException(nameof(headerName));

                if (headerName.Length == 0)
                    throw ExceptionHelper.EmptyString(nameof(headerName));

                #endregion

                if (!_permanentHeaders.TryGetValue(headerName, out string value))
                    value = string.Empty;

                return value;
            }
            set
            {
                #region Parameter check

                if (headerName == null)
                    throw new ArgumentNullException(nameof(headerName));

                if (headerName.Length == 0)
                    throw ExceptionHelper.EmptyString(nameof(headerName));

                #endregion

                if (string.IsNullOrEmpty(value))
                    _permanentHeaders.Remove(headerName);
                else
                    _permanentHeaders[headerName] = value;
            }
        }

        /// <summary>
        /// Gets or sets the HTTP header value.
        /// </summary>
        /// <param name="header">HTTP header.</param>
        /// <value>The value of the HTTP header, if one is set, otherwise the empty string. If you set the value <see langword="null"/> or an empty string, the HTTP header will be removed from the list.</value>
        /// <exception cref="System.ArgumentException">Setting the value of the HTTP header, which must be set using a custom property/method.</exception>
        /// <remarks>List of HTTP headers that should only be set using special properties/methods:
        /// <list type="table">
        ///     <item>
        ///        <description>Accept-Encoding</description>
        ///     </item>
        ///     <item>
        ///        <description>Content-Length</description>
        ///     </item>
        ///     <item>
        ///         <description>Content-Type</description>
        ///     </item>
        ///     <item>
        ///        <description>Connection</description>
        ///     </item>
        ///     <item>
        ///        <description>Proxy-Connection</description>
        ///     </item>
        ///     <item>
        ///        <description>Host</description>
        ///     </item>
        /// </list>
        /// </remarks>
        public string this[HttpHeader header]
        {
            get => this[Utility.Headers[header]];
            set => this[Utility.Headers[header]] = value;
        }

        #endregion

        #region Constructors (public)

        /// <summary>
        /// Initializes a new instance of the class <see cref="HttpRequest"/>.
        /// </summary>
        public HttpRequest()
        {
            Init();
        }

        #endregion

        #region Methods (public)

        #region Get

        /// <summary>
        /// Sends a GET request to an HTTP server.
        /// </summary>
        /// <param name="address">Internet resource address.</param>
        /// <param name="urlParams">URL parameters, or value <see langword="null"/>.</param>
        /// <returns>An object for downloading a response from an HTTP server.</returns>
        /// <exception cref="System.ArgumentNullException">Parameter value <paramref name="address"/> equals <see langword="null"/>.</exception>
        /// <exception cref="System.ArgumentException">Parameter value <paramref name="address"/> is an empty string.</exception>
        /// <exception cref="MVNet.HttpException">Error while working with HTTP protocol.</exception>
        public HttpResponse Get(string address, RequestParams urlParams = null)
        {
            if (urlParams != null)
            {
                var uriBuilder = new UriBuilder(address)
                {
                    Query = urlParams.Query
                };
                address = uriBuilder.Uri.AbsoluteUri;
            }

            return Raw(HttpMethod.Get, address);
        }

        /// <summary>
        /// Sends a GET request to an HTTP server.
        /// </summary>
        /// <param name="address">Internet resource address.</param>
        /// <param name="urlParams">URL parameters, or value <see langword="null"/>.</param>
        /// <returns>An object for downloading a response from an HTTP server.</returns>
        /// <exception cref="System.ArgumentNullException">Parameter value <paramref name="address"/> equals <see langword="null"/>.</exception>
        /// <exception cref="MVNet.HttpException">Error while working with HTTP protocol.</exception>
        public HttpResponse Get(Uri address, RequestParams urlParams = null)
        {
            if (urlParams != null)
            {
                var uriBuilder = new UriBuilder(address)
                {
                    Query = urlParams.Query
                };
                address = uriBuilder.Uri;
            }

            return Raw(HttpMethod.Get, address);
        }

        #endregion

        #region Head

        /// <summary>
        /// Sends a HEAD request to an HTTP server.
        /// </summary>
        /// <param name="address">Internet resource address.</param>
        /// <param name="urlParams">URL parameters, or value <see langword="null"/>.</param>
        /// <returns>An object for downloading a response from an HTTP server.</returns>
        /// <exception cref="System.ArgumentNullException">Parameter value <paramref name="address"/> equals <see langword="null"/>.</exception>
        /// <exception cref="System.ArgumentException">Parameter value <paramref name="address"/> is an empty string.</exception>
        /// <exception cref="MVNet.HttpException">Error while working with HTTP protocol.</exception>
        public HttpResponse Head(string address, RequestParams urlParams = null)
        {
            if (urlParams != null)
            {
                var uriBuilder = new UriBuilder(address)
                {
                    Query = urlParams.Query
                };
                address = uriBuilder.Uri.AbsoluteUri;
            }

            return Raw(HttpMethod.Head, address);
        }

        /// <summary>
        /// Sends a HEAD request to an HTTP server.
        /// </summary>
        /// <param name="address">Internet resource address.</param>
        /// <param name="urlParams">URL parameters, or value <see langword="null"/>.</param>
        /// <returns>An object for downloading a response from an HTTP server.</returns>
        /// <exception cref="System.ArgumentNullException">Parameter value <paramref name="address"/> equals <see langword="null"/>.</exception>
        /// <exception cref="MVNet.HttpException">Error while working with HTTP protocol.</exception>
        public HttpResponse Head(Uri address, RequestParams urlParams = null)
        {
            if (urlParams != null)
            {
                var uriBuilder = new UriBuilder(address)
                {
                    Query = urlParams.Query
                };
                address = uriBuilder.Uri;
            }

            return Raw(HttpMethod.Head, address);
        }

        #endregion

        #region Options

        /// <summary>
        /// Sends an OPTIONS request to an HTTP server.
        /// </summary>
        /// <param name="address">Internet resource address.</param>
        /// <param name="urlParams">URL parameters, or value <see langword="null"/>.</param>
        /// <returns>An object for downloading a response from an HTTP server.</returns>
        /// <exception cref="System.ArgumentNullException">Parameter value <paramref name="address"/> equals <see langword="null"/>.</exception>
        /// <exception cref="System.ArgumentException">Parameter value <paramref name="address"/> is an empty string.</exception>
        /// <exception cref="MVNet.HttpException">Error while working with HTTP protocol.</exception>
        public HttpResponse Options(string address, RequestParams urlParams = null)
        {
            if (urlParams != null)
            {
                var uriBuilder = new UriBuilder(address)
                {
                    Query = urlParams.Query
                };
                address = uriBuilder.Uri.AbsoluteUri;
            }

            return Raw(HttpMethod.Options, address);
        }

        /// <summary>
        /// Sends an OPTIONS request to an HTTP server.
        /// </summary>
        /// <param name="address">Internet resource address.</param>
        /// <param name="urlParams">URL parameters, or value <see langword="null"/>.</param>
        /// <returns>An object for downloading a response from an HTTP server.</returns>
        /// <exception cref="System.ArgumentNullException">Parameter value <paramref name="address"/> equals <see langword="null"/>.</exception>
        /// <exception cref="MVNet.HttpException">Error while working with HTTP protocol.</exception>
        public HttpResponse Options(Uri address, RequestParams urlParams = null)
        {
            if (urlParams != null)
            {
                var uriBuilder = new UriBuilder(address)
                {
                    Query = urlParams.Query
                };
                address = uriBuilder.Uri;
            }

            return Raw(HttpMethod.Options, address);
        }

        #endregion

        #region Post

        /// <summary>
        /// Sends a POST request to an HTTP server.
        /// </summary>
        /// <param name="address">Internet resource address.</param>
        /// <returns>An object for downloading a response from an HTTP server.</returns>
        /// <exception cref="System.ArgumentNullException">Parameter value <paramref name="address"/> equals <see langword="null"/>.</exception>
        /// <exception cref="System.ArgumentException">Parameter value <paramref name="address"/> is an empty string.</exception>
        /// <exception cref="MVNet.HttpException">Error while working with HTTP protocol.</exception>
        public HttpResponse Post(string address)
        {
            return Raw(HttpMethod.Post, address);
        }

        /// <summary>
        /// Sends a POST request to an HTTP server.
        /// </summary>
        /// <param name="address">Internet resource address.</param>
        /// <returns>An object for downloading a response from an HTTP server.</returns>
        /// <exception cref="System.ArgumentNullException">Parameter value <paramref name="address"/> equals <see langword="null"/>.</exception>
        /// <exception cref="MVNet.HttpException">Error while working with HTTP protocol.</exception>
        public HttpResponse Post(Uri address)
        {
            return Raw(HttpMethod.Post, address);
        }

        /// <summary>
        /// Sends a POST request to an HTTP server.
        /// </summary>
        /// <param name="address">Internet resource address.</param>
        /// <param name="reqParams">Request parameters sent to the HTTP server.</param>
        /// <returns>An object for downloading a response from an HTTP server.</returns>
        /// <exception cref="System.ArgumentNullException">
        /// Parameter value <paramref name="address"/> equals <see langword="null"/>.
        /// -or-
        /// Parameter value <paramref name="reqParams"/> equals <see langword="null"/>.
        /// </exception>
        /// <exception cref="System.ArgumentException">Parameter value <paramref name="address"/> is an empty string.</exception>
        /// <exception cref="MVNet.HttpException">Error while working with HTTP protocol.</exception>
        public HttpResponse Post(string address, RequestParams reqParams)
        {
            #region Parameter Check

            if (reqParams == null)
                throw new ArgumentNullException(nameof(reqParams));

            #endregion

            return Raw(HttpMethod.Post, address, new FormUrlEncodedContent(reqParams));
        }


        /// <summary>
        /// Sends a POST request to an HTTP server.
        /// </summary>
        /// <param name="address">Internet resource address.</param>
        /// <param name="reqParams">Request parameters sent to the HTTP server.</param>
        /// <returns>An object for downloading a response from an HTTP server.</returns>
        /// <exception cref="System.ArgumentNullException">
        /// Parameter value <paramref name="address"/> equals <see langword="null"/>.
        /// -or-
        /// Parameter value <paramref name="reqParams"/> equals <see langword="null"/>.
        /// </exception>
        /// <exception cref="MVNet.HttpException">Error while working with HTTP protocol.</exception>
        public HttpResponse Post(Uri address, RequestParams reqParams)
        {
            #region Parameter Check

            if (reqParams == null)
                throw new ArgumentNullException(nameof(reqParams));

            #endregion

            return Raw(HttpMethod.Post, address, new FormUrlEncodedContent(reqParams));
        }

        /// <summary>
        /// Sends a POST request to an HTTP server.
        /// </summary>
        /// <param name="address">Internet resource address.</param>
        /// <param name="str">The string sent to the HTTP server.</param>
        /// <param name="contentType">The type of data being sent.</param>
        /// <returns>An object for downloading a response from an HTTP server.</returns>
        /// <exception cref="System.ArgumentNullException">
        /// Parameter value <paramref name="address"/> equals <see langword="null"/>.
        /// -or-
        /// Parameter value <paramref name="str"/> equals <see langword="null"/>.
        /// -or-
        /// Parameter value <paramref name="contentType"/> equals <see langword="null"/>.
        /// </exception>
        /// <exception cref="System.ArgumentException">
        /// Parameter value <paramref name="address"/> is an empty string.
        /// -or-
        /// Parameter value <paramref name="str"/> is an empty string.
        /// -or
        /// Parameter value <paramref name="contentType"/> is an empty string.
        /// </exception>
        /// <exception cref="MVNet.HttpException">Error while working with HTTP protocol.</exception>
        public HttpResponse Post(string address, string str, string contentType)
        {
            #region Parameter Check

            if (str == null)
                throw new ArgumentNullException(nameof(str));

            if (str.Length == 0)
                throw new ArgumentNullException(nameof(str));

            if (contentType == null)
                throw new ArgumentNullException(nameof(contentType));

            if (contentType.Length == 0)
                throw new ArgumentNullException(nameof(contentType));

            #endregion

            var content = new StringContent(str)
            {
                ContentType = contentType
            };

            return Raw(HttpMethod.Post, address, content);
        }

        /// <summary>
        /// Sends a POST request to an HTTP server.
        /// </summary>
        /// <param name="address">Internet resource address.</param>
        /// <param name="str">The string sent to the HTTP server.</param>
        /// <param name="contentType">The type of data being sent.</param>
        /// <returns>An object for downloading a response from an HTTP server.</returns>
        /// <exception cref="System.ArgumentNullException">
        /// Parameter value <paramref name="address"/> equals <see langword="null"/>.
        /// -or-
        /// Parameter value <paramref name="str"/> equals <see langword="null"/>.
        /// -or-
        /// Parameter value <paramref name="contentType"/> equals <see langword="null"/>.
        /// </exception>
        /// <exception cref="System.ArgumentException">
        /// Parameter value <paramref name="str"/> is an empty string.
        /// -or-
        /// Parameter value <paramref name="contentType"/> is an empty string.
        /// </exception>
        /// <exception cref="MVNet.HttpException">Error while working with HTTP protocol.</exception>
        public HttpResponse Post(Uri address, string str, string contentType)
        {
            #region Parameter Check

            if (str == null)
                throw new ArgumentNullException(nameof(str));

            if (str.Length == 0)
                throw new ArgumentNullException(nameof(str));

            if (contentType == null)
                throw new ArgumentNullException(nameof(contentType));

            if (contentType.Length == 0)
                throw new ArgumentNullException(nameof(contentType));

            #endregion

            var content = new StringContent(str)
            {
                ContentType = contentType
            };

            return Raw(HttpMethod.Post, address, content);
        }

        /// <summary>
        /// Sends a POST request to an HTTP server.
        /// </summary>
        /// <param name="address">Internet resource address.</param>
        /// <param name="bytes">An array of bytes sent to the HTTP server.</param>
        /// <param name="contentType">The type of data being sent.</param>
        /// <returns>An object for downloading a response from an HTTP server.</returns>
        /// <exception cref="System.ArgumentNullException">
        /// Parameter value <paramref name="address"/> equals <see langword="null"/>.
        /// -or-
        /// Parameter value <paramref name="bytes"/> equals <see langword="null"/>.
        /// -or-
        /// Parameter value <paramref name="contentType"/> equals <see langword="null"/>.
        /// </exception>
        /// <exception cref="System.ArgumentException">
        /// Parameter value <paramref name="address"/> is an empty string.
        /// -or-
        /// Parameter value <paramref name="contentType"/> is an empty string.
        /// </exception>
        /// <exception cref="MVNet.HttpException">Error while working with HTTP protocol.</exception>
        public HttpResponse Post(string address, byte[] bytes, string contentType = "application/octet-stream")
        {
            #region Parameter Check

            if (bytes == null)
                throw new ArgumentNullException(nameof(bytes));

            if (contentType == null)
                throw new ArgumentNullException(nameof(contentType));

            if (contentType.Length == 0)
                throw new ArgumentNullException(nameof(contentType));

            #endregion

            var content = new BytesContent(bytes)
            {
                ContentType = contentType
            };

            return Raw(HttpMethod.Post, address, content);
        }

        /// <summary>
        /// Sends a POST request to an HTTP server.
        /// </summary>
        /// <param name="address">Internet resource address.</param>
        /// <param name="bytes">An array of bytes sent to the HTTP server.</param>
        /// <param name="contentType">The type of data being sent.</param>
        /// <returns>An object for downloading a response from an HTTP server.</returns>
        /// <exception cref="System.ArgumentNullException">
        /// Parameter value <paramref name="address"/> equals <see langword="null"/>.
        /// -or-
        /// Parameter value <paramref name="bytes"/> equals <see langword="null"/>.
        /// -or-
        /// Parameter value <paramref name="contentType"/> equals <see langword="null"/>.
        /// </exception>
        /// <exception cref="System.ArgumentException">Parameter value <paramref name="contentType"/> is an empty string.</exception>
        /// <exception cref="MVNet.HttpException">Error while working with HTTP protocol.</exception>
        public HttpResponse Post(Uri address, byte[] bytes, string contentType = "application/octet-stream")
        {
            #region Parameter Check

            if (bytes == null)
                throw new ArgumentNullException(nameof(bytes));

            if (contentType == null)
                throw new ArgumentNullException(nameof(contentType));

            if (contentType.Length == 0)
                throw new ArgumentNullException(nameof(contentType));

            var content = new BytesContent(bytes)
            {
                ContentType = contentType
            };

            #endregion

            return Raw(HttpMethod.Post, address, content);
        }

        /// <summary>
        /// Sends a POST request to an HTTP server.
        /// </summary>
        /// <param name="address">Internet resource address.</param>
        /// <param name="stream">The data stream sent to the HTTP server.</param>
        /// <param name="contentType">The type of data being sent.</param>
        /// <returns>An object for downloading a response from an HTTP server.</returns>
        /// <exception cref="System.ArgumentNullException">
        /// Parameter value <paramref name="address"/> equals <see langword="null"/>.
        /// -or-
        /// Parameter value <paramref name="stream"/> equals <see langword="null"/>.
        /// -or-
        /// Parameter value <paramref name="contentType"/> equals <see langword="null"/>.
        /// </exception>
        /// <exception cref="System.ArgumentException">
        /// Parameter value <paramref name="address"/> is an empty string.
        /// -or-
        /// Parameter value <paramref name="contentType"/> is an empty string.
        /// </exception>
        /// <exception cref="MVNet.HttpException">Error while working with HTTP protocol.</exception>
        public HttpResponse Post(string address, Stream stream, string contentType = "application/octet-stream")
        {
            #region Parameter Check

            if (stream == null)
                throw new ArgumentNullException(nameof(stream));

            if (contentType == null)
                throw new ArgumentNullException(nameof(contentType));

            if (contentType.Length == 0)
                throw new ArgumentNullException(nameof(contentType));

            #endregion

            var content = new StreamContent(stream)
            {
                ContentType = contentType
            };

            return Raw(HttpMethod.Post, address, content);
        }

        /// <summary>
        /// Sends a POST request to an HTTP server.
        /// </summary>
        /// <param name="address">Internet resource address.</param>
        /// <param name="stream">The data stream sent to the HTTP server.</param>
        /// <param name="contentType">The type of data being sent.</param>
        /// <returns>An object for downloading a response from an HTTP server.</returns>
        /// <exception cref="System.ArgumentNullException">
        /// Parameter value <paramref name="address"/> equals <see langword="null"/>.
        /// -or-
        /// Parameter value <paramref name="stream"/> equals <see langword="null"/>.
        /// -or-
        /// Parameter value <paramref name="contentType"/> equals <see langword="null"/>.
        /// </exception>
        /// <exception cref="System.ArgumentException">Parameter value <paramref name="contentType"/> is an empty string.</exception>
        /// <exception cref="MVNet.HttpException">Error while working with HTTP protocol.</exception>
        public HttpResponse Post(Uri address, Stream stream, string contentType = "application/octet-stream")
        {
            #region Parameter Check

            if (stream == null)
                throw new ArgumentNullException(nameof(stream));

            if (contentType == null)
                throw new ArgumentNullException(nameof(contentType));

            if (contentType.Length == 0)
                throw new ArgumentNullException(nameof(contentType));

            #endregion

            var content = new StreamContent(stream)
            {
                ContentType = contentType
            };

            return Raw(HttpMethod.Post, address, content);
        }

        /// <summary>
        /// Sends a POST request to an HTTP server.
        /// </summary>
        /// <param name="address">Internet resource address.</param>
        /// <param name="path">The path to the file whose data will be sent to the HTTP server.</param>
        /// <returns>An object for downloading a response from an HTTP server.</returns>
        /// <exception cref="System.ArgumentNullException">
        /// Parameter value <paramref name="address"/> equals <see langword="null"/>.
        /// -or-
        /// Parameter value <paramref name="path"/> equals <see langword="null"/>.
        /// </exception>
        /// <exception cref="System.ArgumentException">
        /// Parameter value <paramref name="address"/> is an empty string.
        /// -or-
        /// Parameter value <paramref name="path"/> is an empty string.
        /// </exception>
        /// <exception cref="MVNet.HttpException">Error while working with HTTP protocol.</exception>
        public HttpResponse Post(string address, string path)
        {
            #region Parameter Check

            if (path == null)
                throw new ArgumentNullException(nameof(path));

            if (path.Length == 0)
                throw new ArgumentNullException(nameof(path));

            #endregion

            return Raw(HttpMethod.Post, address, new FileContent(path));
        }

        /// <summary>
        /// Sends a POST request to an HTTP server.
        /// </summary>
        /// <param name="address">Internet resource address.</param>
        /// <param name="path">The path to the file whose data will be sent to the HTTP server.</param>
        /// <returns>An object for downloading a response from an HTTP server.</returns>
        /// <exception cref="System.ArgumentNullException">
        /// Parameter value <paramref name="address"/> equals <see langword="null"/>.
        /// -or-
        /// Parameter value <paramref name="path"/> equals <see langword="null"/>.
        /// </exception>
        /// <exception cref="System.ArgumentException">Parameter value <paramref name="path"/> is an empty string.</exception>
        /// <exception cref="MVNet.HttpException">Error while working with HTTP protocol.</exception>
        public HttpResponse Post(Uri address, string path)
        {
            #region Parameter Check

            if (path == null)
                throw new ArgumentNullException(nameof(path));

            if (path.Length == 0)
                throw new ArgumentNullException(nameof(path));

            #endregion

            return Raw(HttpMethod.Post, address, new FileContent(path));
        }

        /// <summary>
        /// Sends a POST request to an HTTP server.
        /// </summary>
        /// <param name="address">Internet resource address.</param>
        /// <param name="content">Content sent to the HTTP server.</param>
        /// <returns>An object for downloading a response from an HTTP server.</returns>
        /// <exception cref="System.ArgumentNullException">
        /// Parameter value <paramref name="address"/> equals <see langword="null"/>.
        /// -or-
        /// Parameter value <paramref name="content"/> equals <see langword="null"/>.
        /// </exception>
        /// <exception cref="System.ArgumentException">Parameter value <paramref name="address"/> is an empty string.</exception>
        /// <exception cref="MVNet.HttpException">Error while working with HTTP protocol.</exception>
        public HttpResponse Post(string address, HttpContent content)
        {
            #region Parameter Check

            if (content == null)
                throw new ArgumentNullException(nameof(content));

            #endregion

            return Raw(HttpMethod.Post, address, content);
        }

        /// <summary>
        /// Sends a POST request to an HTTP server.
        /// </summary>
        /// <param name="address">Internet resource address.</param>
        /// <param name="content">Content sent to the HTTP server.</param>
        /// <returns>An object for downloading a response from an HTTP server.</returns>
        /// <exception cref="System.ArgumentNullException">
        /// Parameter value <paramref name="address"/> equals <see langword="null"/>.
        /// -or-
        /// Parameter value <paramref name="content"/> equals <see langword="null"/>.
        /// </exception>
        /// <exception cref="MVNet.HttpException">Error while working with HTTP protocol.</exception>
        public HttpResponse Post(Uri address, HttpContent content)
        {
            #region Parameter Check

            if (content == null)
                throw new ArgumentNullException(nameof(content));

            #endregion

            return Raw(HttpMethod.Post, address, content);
        }

        #endregion

        #region Patch

        /// <summary>
        /// Sends a PATCH request to an HTTP server.
        /// </summary>
        /// <param name="address">Internet resource address.</param>
        /// <returns>An object for downloading a response from an HTTP server.</returns>
        /// <exception cref="System.ArgumentNullException">Parameter value <paramref name="address"/> equals <see langword="null"/>.</exception>
        /// <exception cref="System.ArgumentException">Parameter value <paramref name="address"/> is an empty string.</exception>
        /// <exception cref="MVNet.HttpException">Error while working with HTTP protocol.</exception>
        public HttpResponse Patch(string address)
        {
            return Raw(HttpMethod.Patch, address);
        }

        /// <summary>
        /// Sends a PATCH request to an HTTP server.
        /// </summary>
        /// <param name="address">Internet resource address.</param>
        /// <returns>An object for downloading a response from an HTTP server.</returns>
        /// <exception cref="System.ArgumentNullException">Parameter value <paramref name="address"/> equals <see langword="null"/>.</exception>
        /// <exception cref="MVNet.HttpException">Error while working with HTTP protocol.</exception>
        public HttpResponse Patch(Uri address)
        {
            return Raw(HttpMethod.Patch, address);
        }

        /// <summary>
        /// Sends a PATCH request to an HTTP server.
        /// </summary>
        /// <param name="address">Internet resource address.</param>
        /// <param name="reqParams">Request parameters sent to the HTTP server.</param>
        /// <returns>An object for downloading a response from an HTTP server.</returns>
        /// <exception cref="System.ArgumentNullException">
        /// Parameter value <paramref name="address"/> equals <see langword="null"/>.
        /// -or-
        /// Parameter value <paramref name="reqParams"/> equals <see langword="null"/>.
        /// </exception>
        /// <exception cref="System.ArgumentException">Parameter value <paramref name="address"/> is an empty string.</exception>
        /// <exception cref="MVNet.HttpException">Error while working with HTTP protocol.</exception>
        public HttpResponse Patch(string address, RequestParams reqParams)
        {
            #region Parameter Check

            if (reqParams == null)
                throw new ArgumentNullException(nameof(reqParams));

            #endregion

            return Raw(HttpMethod.Patch, address, new FormUrlEncodedContent(reqParams));
        }


        /// <summary>
        /// Sends a PATCH request to an HTTP server.
        /// </summary>
        /// <param name="address">Internet resource address.</param>
        /// <param name="reqParams">Request parameters sent to the HTTP server.</param>
        /// <returns>An object for downloading a response from an HTTP server.</returns>
        /// <exception cref="System.ArgumentNullException">
        /// Parameter value <paramref name="address"/> equals <see langword="null"/>.
        /// -or-
        /// Parameter value <paramref name="reqParams"/> equals <see langword="null"/>.
        /// </exception>
        /// <exception cref="MVNet.HttpException">Error while working with HTTP protocol.</exception>
        public HttpResponse Patch(Uri address, RequestParams reqParams)
        {
            #region Parameter Check

            if (reqParams == null)
                throw new ArgumentNullException(nameof(reqParams));

            #endregion

            return Raw(HttpMethod.Patch, address, new FormUrlEncodedContent(reqParams));
        }

        /// <summary>
        /// Sends a PATCH request to an HTTP server.
        /// </summary>
        /// <param name="address">Internet resource address.</param>
        /// <param name="str">The string sent to the HTTP server.</param>
        /// <param name="contentType">The type of data being sent.</param>
        /// <returns>An object for downloading a response from an HTTP server.</returns>
        /// <exception cref="System.ArgumentNullException">
        /// Parameter value <paramref name="address"/> equals <see langword="null"/>.
        /// -or-
        /// Parameter value <paramref name="str"/> equals <see langword="null"/>.
        /// -or-
        /// Parameter value <paramref name="contentType"/> equals <see langword="null"/>.
        /// </exception>
        /// <exception cref="System.ArgumentException">
        /// Parameter value <paramref name="address"/> is an empty string.
        /// -or-
        /// Parameter value <paramref name="str"/> is an empty string.
        /// -or
        /// Parameter value <paramref name="contentType"/> is an empty string.
        /// </exception>
        /// <exception cref="MVNet.HttpException">Error while working with HTTP protocol.</exception>
        public HttpResponse Patch(string address, string str, string contentType)
        {
            #region Parameter Check

            if (str == null)
                throw new ArgumentNullException(nameof(str));

            if (str.Length == 0)
                throw new ArgumentNullException(nameof(str));

            if (contentType == null)
                throw new ArgumentNullException(nameof(contentType));

            if (contentType.Length == 0)
                throw new ArgumentNullException(nameof(contentType));

            #endregion

            var content = new StringContent(str)
            {
                ContentType = contentType
            };

            return Raw(HttpMethod.Patch, address, content);
        }

        /// <summary>
        /// Sends a PATCH request to an HTTP server.
        /// </summary>
        /// <param name="address">Internet resource address.</param>
        /// <param name="str">The string sent to the HTTP server.</param>
        /// <param name="contentType">The type of data being sent.</param>
        /// <returns>An object for downloading a response from an HTTP server.</returns>
        /// <exception cref="System.ArgumentNullException">
        /// Parameter value <paramref name="address"/> equals <see langword="null"/>.
        /// -or-
        /// Parameter value <paramref name="str"/> equals <see langword="null"/>.
        /// -or-
        /// Parameter value <paramref name="contentType"/> equals <see langword="null"/>.
        /// </exception>
        /// <exception cref="System.ArgumentException">
        /// Parameter value <paramref name="str"/> is an empty string.
        /// -or-
        /// Parameter value <paramref name="contentType"/> is an empty string.
        /// </exception>
        /// <exception cref="MVNet.HttpException">Error while working with HTTP protocol.</exception>
        public HttpResponse Patch(Uri address, string str, string contentType)
        {
            #region Parameter Check

            if (str == null)
                throw new ArgumentNullException(nameof(str));

            if (str.Length == 0)
                throw new ArgumentNullException(nameof(str));

            if (contentType == null)
                throw new ArgumentNullException(nameof(contentType));

            if (contentType.Length == 0)
                throw new ArgumentNullException(nameof(contentType));

            #endregion

            var content = new StringContent(str)
            {
                ContentType = contentType
            };

            return Raw(HttpMethod.Patch, address, content);
        }

        /// <summary>
        /// Sends a PATCH request to an HTTP server.
        /// </summary>
        /// <param name="address">Internet resource address.</param>
        /// <param name="bytes">An array of bytes sent to the HTTP server.</param>
        /// <param name="contentType">The type of data being sent.</param>
        /// <returns>An object for downloading a response from an HTTP server.</returns>
        /// <exception cref="System.ArgumentNullException">
        /// Parameter value <paramref name="address"/> equals <see langword="null"/>.
        /// -or-
        /// Parameter value <paramref name="bytes"/> equals <see langword="null"/>.
        /// -or-
        /// Parameter value <paramref name="contentType"/> equals <see langword="null"/>.
        /// </exception>
        /// <exception cref="System.ArgumentException">
        /// Parameter value <paramref name="address"/> is an empty string.
        /// -or-
        /// Parameter value <paramref name="contentType"/> is an empty string.
        /// </exception>
        /// <exception cref="MVNet.HttpException">Error while working with HTTP protocol.</exception>
        public HttpResponse Patch(string address, byte[] bytes, string contentType = "application/octet-stream")
        {
            #region Parameter Check

            if (bytes == null)
                throw new ArgumentNullException(nameof(bytes));

            if (contentType == null)
                throw new ArgumentNullException(nameof(contentType));

            if (contentType.Length == 0)
                throw new ArgumentNullException(nameof(contentType));

            #endregion

            var content = new BytesContent(bytes)
            {
                ContentType = contentType
            };

            return Raw(HttpMethod.Patch, address, content);
        }

        /// <summary>
        /// Sends a PATCH request to an HTTP server.
        /// </summary>
        /// <param name="address">Internet resource address.</param>
        /// <param name="bytes">An array of bytes sent to the HTTP server.</param>
        /// <param name="contentType">The type of data being sent.</param>
        /// <returns>An object for downloading a response from an HTTP server.</returns>
        /// <exception cref="System.ArgumentNullException">
        /// Parameter value <paramref name="address"/> equals <see langword="null"/>.
        /// -or-
        /// Parameter value <paramref name="bytes"/> equals <see langword="null"/>.
        /// -or-
        /// Parameter value <paramref name="contentType"/> equals <see langword="null"/>.
        /// </exception>
        /// <exception cref="System.ArgumentException">Parameter value <paramref name="contentType"/> is an empty string.</exception>
        /// <exception cref="MVNet.HttpException">Error while working with HTTP protocol.</exception>
        public HttpResponse Patch(Uri address, byte[] bytes, string contentType = "application/octet-stream")
        {
            #region Parameter Check

            if (bytes == null)
                throw new ArgumentNullException(nameof(bytes));

            if (contentType == null)
                throw new ArgumentNullException(nameof(contentType));

            if (contentType.Length == 0)
                throw new ArgumentNullException(nameof(contentType));

            #endregion

            var content = new BytesContent(bytes)
            {
                ContentType = contentType
            };

            return Raw(HttpMethod.Patch, address, content);
        }

        /// <summary>
        /// Sends a PATCH request to an HTTP server.
        /// </summary>
        /// <param name="address">Internet resource address.</param>
        /// <param name="stream">The data stream sent to the HTTP server.</param>
        /// <param name="contentType">The type of data being sent.</param>
        /// <returns>An object for downloading a response from an HTTP server.</returns>
        /// <exception cref="System.ArgumentNullException">
        /// Parameter value <paramref name="address"/> equals <see langword="null"/>.
        /// -or-
        /// Parameter value <paramref name="stream"/> equals <see langword="null"/>.
        /// -or-
        /// Parameter value <paramref name="contentType"/> equals <see langword="null"/>.
        /// </exception>
        /// <exception cref="System.ArgumentException">
        /// Parameter value <paramref name="address"/> is an empty string.
        /// -or-
        /// Parameter value <paramref name="contentType"/> is an empty string.
        /// </exception>
        /// <exception cref="MVNet.HttpException">Error while working with HTTP protocol.</exception>
        public HttpResponse Patch(string address, Stream stream, string contentType = "application/octet-stream")
        {
            #region Parameter Check

            if (stream == null)
                throw new ArgumentNullException(nameof(stream));

            if (contentType == null)
                throw new ArgumentNullException(nameof(contentType));

            if (contentType.Length == 0)
                throw new ArgumentNullException(nameof(contentType));

            #endregion

            var content = new StreamContent(stream)
            {
                ContentType = contentType
            };

            return Raw(HttpMethod.Patch, address, content);
        }

        /// <summary>
        /// Sends a PATCH request to an HTTP server.
        /// </summary>
        /// <param name="address">Internet resource address.</param>
        /// <param name="stream">The data stream sent to the HTTP server.</param>
        /// <param name="contentType">The type of data being sent.</param>
        /// <returns>An object for downloading a response from an HTTP server.</returns>
        /// <exception cref="System.ArgumentNullException">
        /// Parameter value <paramref name="address"/> equals <see langword="null"/>.
        /// -or-
        /// Parameter value <paramref name="stream"/> equals <see langword="null"/>.
        /// -or-
        /// Parameter value <paramref name="contentType"/> equals <see langword="null"/>.
        /// </exception>
        /// <exception cref="System.ArgumentException">Parameter value <paramref name="contentType"/> is an empty string.</exception>
        /// <exception cref="MVNet.HttpException">Error while working with HTTP protocol.</exception>
        public HttpResponse Patch(Uri address, Stream stream, string contentType = "application/octet-stream")
        {
            #region Parameter Check

            if (stream == null)
                throw new ArgumentNullException(nameof(stream));

            if (contentType == null)
                throw new ArgumentNullException(nameof(contentType));

            if (contentType.Length == 0)
                throw new ArgumentNullException(nameof(contentType));

            #endregion

            var content = new StreamContent(stream)
            {
                ContentType = contentType
            };

            return Raw(HttpMethod.Patch, address, content);
        }

        /// <summary>
        /// Sends a PATCH request to an HTTP server.
        /// </summary>
        /// <param name="address">Internet resource address.</param>
        /// <param name="path">The path to the file whose data will be sent to the HTTP server.</param>
        /// <returns>An object for downloading a response from an HTTP server.</returns>
        /// <exception cref="System.ArgumentNullException">
        /// Parameter value <paramref name="address"/> equals <see langword="null"/>.
        /// -or-
        /// Parameter value <paramref name="path"/> equals <see langword="null"/>.
        /// </exception>
        /// <exception cref="System.ArgumentException">
        /// Parameter value <paramref name="address"/> is an empty string.
        /// -or-
        /// Parameter value <paramref name="path"/> is an empty string.
        /// </exception>
        /// <exception cref="MVNet.HttpException">Error while working with HTTP protocol.</exception>
        public HttpResponse Patch(string address, string path)
        {
            #region Parameter Check

            if (path == null)
                throw new ArgumentNullException(nameof(path));

            if (path.Length == 0)
                throw new ArgumentNullException(nameof(path));

            #endregion

            return Raw(HttpMethod.Patch, address, new FileContent(path));
        }

        /// <summary>
        /// Sends a PATCH request to an HTTP server.
        /// </summary>
        /// <param name="address">Internet resource address.</param>
        /// <param name="path">The path to the file whose data will be sent to the HTTP server.</param>
        /// <returns>An object for downloading a response from an HTTP server.</returns>
        /// <exception cref="System.ArgumentNullException">
        /// Parameter value <paramref name="address"/> equals <see langword="null"/>.
        /// -or-
        /// Parameter value <paramref name="path"/> equals <see langword="null"/>.
        /// </exception>
        /// <exception cref="System.ArgumentException">Parameter value <paramref name="path"/> is an empty string.</exception>
        /// <exception cref="MVNet.HttpException">Error while working with HTTP protocol.</exception>
        public HttpResponse Patch(Uri address, string path)
        {
            #region Parameter Check

            if (path == null)
                throw new ArgumentNullException(nameof(path));

            if (path.Length == 0)
                throw new ArgumentNullException(nameof(path));

            #endregion

            return Raw(HttpMethod.Patch, address, new FileContent(path));
        }

        /// <summary>
        /// Sends a PATCH request to an HTTP server.
        /// </summary>
        /// <param name="address">Internet resource address.</param>
        /// <param name="content">Content sent to the HTTP server.</param>
        /// <returns>An object for downloading a response from an HTTP server.</returns>
        /// <exception cref="System.ArgumentNullException">
        /// Parameter value <paramref name="address"/> equals <see langword="null"/>.
        /// -or-
        /// Parameter value <paramref name="content"/> equals <see langword="null"/>.
        /// </exception>
        /// <exception cref="System.ArgumentException">Parameter value <paramref name="address"/> is an empty string.</exception>
        /// <exception cref="MVNet.HttpException">Error while working with HTTP protocol.</exception>
        public HttpResponse Patch(string address, HttpContent content)
        {
            #region Parameter Check

            if (content == null)
                throw new ArgumentNullException(nameof(content));

            #endregion

            return Raw(HttpMethod.Patch, address, content);
        }

        /// <summary>
        /// Sends a PATCH request to an HTTP server.
        /// </summary>
        /// <param name="address">Internet resource address.</param>
        /// <param name="content">Content sent to the HTTP server.</param>
        /// <returns>An object for downloading a response from an HTTP server.</returns>
        /// <exception cref="System.ArgumentNullException">
        /// Parameter value <paramref name="address"/> equals <see langword="null"/>.
        /// -or-
        /// Parameter value <paramref name="content"/> equals <see langword="null"/>.
        /// </exception>
        /// <exception cref="MVNet.HttpException">Error while working with HTTP protocol.</exception>
        public HttpResponse Patch(Uri address, HttpContent content)
        {
            #region Parameter Check

            if (content == null)
                throw new ArgumentNullException(nameof(content));

            #endregion

            return Raw(HttpMethod.Patch, address, content);
        }

        #endregion

        #region Put

        /// <summary>
        /// Sends a PUT request to an HTTP server.
        /// </summary>
        /// <param name="address">Internet resource address.</param>
        /// <returns>An object for downloading a response from an HTTP server.</returns>
        /// <exception cref="System.ArgumentNullException">Parameter value <paramref name="address"/> equals <see langword="null"/>.</exception>
        /// <exception cref="System.ArgumentException">Parameter value <paramref name="address"/> is an empty string.</exception>
        /// <exception cref="MVNet.HttpException">Error while working with HTTP protocol.</exception>
        public HttpResponse Put(string address)
        {
            return Raw(HttpMethod.Put, address);
        }

        /// <summary>
        /// Sends a PUT request to an HTTP server.
        /// </summary>
        /// <param name="address">Internet resource address.</param>
        /// <returns>An object for downloading a response from an HTTP server.</returns>
        /// <exception cref="System.ArgumentNullException">Parameter value <paramref name="address"/> equals <see langword="null"/>.</exception>
        /// <exception cref="MVNet.HttpException">Error while working with HTTP protocol.</exception>
        public HttpResponse Put(Uri address)
        {
            return Raw(HttpMethod.Put, address);
        }

        /// <summary>
        /// Sends a PUT request to an HTTP server.
        /// </summary>
        /// <param name="address">Internet resource address.</param>
        /// <param name="reqParams">Request parameters sent to the HTTP server.</param>
        /// <returns>An object for downloading a response from an HTTP server.</returns>
        /// <exception cref="System.ArgumentNullException">
        /// Parameter value <paramref name="address"/> equals <see langword="null"/>.
        /// -or-
        /// Parameter value <paramref name="reqParams"/> equals <see langword="null"/>.
        /// </exception>
        /// <exception cref="System.ArgumentException">Parameter value <paramref name="address"/> is an empty string.</exception>
        /// <exception cref="MVNet.HttpException">Error while working with HTTP protocol.</exception>
        public HttpResponse Put(string address, RequestParams reqParams)
        {
            #region Parameter Check

            if (reqParams == null)
                throw new ArgumentNullException(nameof(reqParams));

            #endregion

            return Raw(HttpMethod.Put, address, new FormUrlEncodedContent(reqParams));
        }


        /// <summary>
        /// Sends a PUT request to an HTTP server.
        /// </summary>
        /// <param name="address">Internet resource address.</param>
        /// <param name="reqParams">Request parameters sent to the HTTP server.</param>
        /// <returns>An object for downloading a response from an HTTP server.</returns>
        /// <exception cref="System.ArgumentNullException">
        /// Parameter value <paramref name="address"/> equals <see langword="null"/>.
        /// -or-
        /// Parameter value <paramref name="reqParams"/> equals <see langword="null"/>.
        /// </exception>
        /// <exception cref="MVNet.HttpException">Error while working with HTTP protocol.</exception>
        public HttpResponse Put(Uri address, RequestParams reqParams)
        {
            #region Parameter Check

            if (reqParams == null)
                throw new ArgumentNullException(nameof(reqParams));

            #endregion

            return Raw(HttpMethod.Put, address, new FormUrlEncodedContent(reqParams));
        }

        /// <summary>
        /// Sends a PUT request to an HTTP server.
        /// </summary>
        /// <param name="address">Internet resource address.</param>
        /// <param name="str">The string sent to the HTTP server.</param>
        /// <param name="contentType">The type of data being sent.</param>
        /// <returns>An object for downloading a response from an HTTP server.</returns>
        /// <exception cref="System.ArgumentNullException">
        /// Parameter value <paramref name="address"/> equals <see langword="null"/>.
        /// -or-
        /// Parameter value <paramref name="str"/> equals <see langword="null"/>.
        /// -or-
        /// Parameter value <paramref name="contentType"/> equals <see langword="null"/>.
        /// </exception>
        /// <exception cref="System.ArgumentException">
        /// Parameter value <paramref name="address"/> is an empty string.
        /// -or-
        /// Parameter value <paramref name="str"/> is an empty string.
        /// -or-
        /// Parameter value <paramref name="contentType"/> is an empty string.
        /// </exception>
        /// <exception cref="MVNet.HttpException">Error while working with HTTP protocol.</exception>
        public HttpResponse Put(string address, string str, string contentType)
        {
            #region Parameter Check

            if (str == null)
                throw new ArgumentNullException(nameof(str));

            if (str.Length == 0)
                throw new ArgumentNullException(nameof(str));

            if (contentType == null)
                throw new ArgumentNullException(nameof(contentType));

            if (contentType.Length == 0)
                throw new ArgumentNullException(nameof(contentType));

            #endregion

            var content = new StringContent(str)
            {
                ContentType = contentType
            };

            return Raw(HttpMethod.Put, address, content);
        }

        /// <summary>
        /// Sends a PUT request to an HTTP server.
        /// </summary>
        /// <param name="address">Internet resource address.</param>
        /// <param name="str">The string sent to the HTTP server.</param>
        /// <param name="contentType">The type of data being sent.</param>
        /// <returns>An object for downloading a response from an HTTP server.</returns>
        /// <exception cref="System.ArgumentNullException">
        /// Parameter value <paramref name="address"/> equals <see langword="null"/>.
        /// -or-
        /// Parameter value <paramref name="str"/> equals <see langword="null"/>.
        /// -or-
        /// Parameter value <paramref name="contentType"/> equals <see langword="null"/>.
        /// </exception>
        /// <exception cref="System.ArgumentException">
        /// Parameter value <paramref name="str"/> is an empty string.
        /// -or-
        /// Parameter value <paramref name="contentType"/> is an empty string.
        /// </exception>
        /// <exception cref="MVNet.HttpException">Error while working with HTTP protocol.</exception>
        public HttpResponse Put(Uri address, string str, string contentType)
        {
            #region Parameter Check

            if (str == null)
                throw new ArgumentNullException(nameof(str));

            if (str.Length == 0)
                throw new ArgumentNullException(nameof(str));

            if (contentType == null)
                throw new ArgumentNullException(nameof(contentType));

            if (contentType.Length == 0)
                throw new ArgumentNullException(nameof(contentType));

            #endregion

            var content = new StringContent(str)
            {
                ContentType = contentType
            };

            return Raw(HttpMethod.Put, address, content);
        }

        /// <summary>
        /// Sends a PUT request to an HTTP server.
        /// </summary>
        /// <param name="address">Internet resource address.</param>
        /// <param name="bytes">An array of bytes sent to the HTTP server.</param>
        /// <param name="contentType">The type of data being sent.</param>
        /// <returns>An object for downloading a response from an HTTP server.</returns>
        /// <exception cref="System.ArgumentNullException">
        /// Parameter value <paramref name="address"/> equals <see langword="null"/>.
        /// -or-
        /// Parameter value <paramref name="bytes"/> equals <see langword="null"/>.
        /// -or-
        /// Parameter value <paramref name="contentType"/> equals <see langword="null"/>.
        /// </exception>
        /// <exception cref="System.ArgumentException">
        /// Parameter value <paramref name="address"/> is an empty string.
        /// -or-
        /// Parameter value <paramref name="contentType"/> is an empty string.
        /// </exception>
        /// <exception cref="MVNet.HttpException">Error while working with HTTP protocol.</exception>
        public HttpResponse Put(string address, byte[] bytes, string contentType = "application/octet-stream")
        {
            #region Parameter Check

            if (bytes == null)
                throw new ArgumentNullException(nameof(bytes));

            if (contentType == null)
                throw new ArgumentNullException(nameof(contentType));

            if (contentType.Length == 0)
                throw new ArgumentNullException(nameof(contentType));

            #endregion

            var content = new BytesContent(bytes)
            {
                ContentType = contentType
            };

            return Raw(HttpMethod.Put, address, content);
        }

        /// <summary>
        /// Sends a PUT request to an HTTP server.
        /// </summary>
        /// <param name="address">Internet resource address.</param>
        /// <param name="bytes">An array of bytes sent to the HTTP server.</param>
        /// <param name="contentType">The type of data being sent.</param>
        /// <returns>An object for downloading a response from an HTTP server.</returns>
        /// <exception cref="System.ArgumentNullException">
        /// Parameter value <paramref name="address"/> equals <see langword="null"/>.
        /// -or-
        /// Parameter value <paramref name="bytes"/> equals <see langword="null"/>.
        /// -or-
        /// Parameter value <paramref name="contentType"/> equals <see langword="null"/>.
        /// </exception>
        /// <exception cref="System.ArgumentException">Parameter value <paramref name="contentType"/> is an empty string.</exception>
        /// <exception cref="MVNet.HttpException">Error while working with HTTP protocol.</exception>
        public HttpResponse Put(Uri address, byte[] bytes, string contentType = "application/octet-stream")
        {
            #region Parameter Check

            if (bytes == null)
                throw new ArgumentNullException(nameof(bytes));

            if (contentType == null)
                throw new ArgumentNullException(nameof(contentType));

            if (contentType.Length == 0)
                throw new ArgumentNullException(nameof(contentType));

            #endregion

            var content = new BytesContent(bytes)
            {
                ContentType = contentType
            };

            return Raw(HttpMethod.Put, address, content);
        }

        /// <summary>
        /// Sends a PUT request to an HTTP server.
        /// </summary>
        /// <param name="address">Internet resource address.</param>
        /// <param name="stream">The data stream sent to the HTTP server.</param>
        /// <param name="contentType">The type of data being sent.</param>
        /// <returns>An object for downloading a response from an HTTP server.</returns>
        /// <exception cref="System.ArgumentNullException">
        /// Parameter value <paramref name="address"/> equals <see langword="null"/>.
        /// -or-
        /// Parameter value <paramref name="stream"/> equals <see langword="null"/>.
        /// -or-
        /// Parameter value <paramref name="contentType"/> equals <see langword="null"/>.
        /// </exception>
        /// <exception cref="System.ArgumentException">
        /// Parameter value <paramref name="address"/> is an empty string.
        /// -or-
        /// Parameter value <paramref name="contentType"/> is an empty string.
        /// </exception>
        /// <exception cref="MVNet.HttpException">Error while working with HTTP protocol.</exception>
        public HttpResponse Put(string address, Stream stream, string contentType = "application/octet-stream")
        {
            #region Parameter Check

            if (stream == null)
                throw new ArgumentNullException(nameof(stream));

            if (contentType == null)
                throw new ArgumentNullException(nameof(contentType));

            if (contentType.Length == 0)
                throw new ArgumentNullException(nameof(contentType));

            #endregion

            var content = new StreamContent(stream)
            {
                ContentType = contentType
            };

            return Raw(HttpMethod.Put, address, content);
        }

        /// <summary>
        /// Sends a PUT request to an HTTP server.
        /// </summary>
        /// <param name="address">Internet resource address.</param>
        /// <param name="stream">The data stream sent to the HTTP server.</param>
        /// <param name="contentType">The type of data being sent.</param>
        /// <returns>An object for downloading a response from an HTTP server.</returns>
        /// <exception cref="System.ArgumentNullException">
        /// Parameter value <paramref name="address"/> equals <see langword="null"/>.
        /// -or-
        /// Parameter value <paramref name="stream"/> equals <see langword="null"/>.
        /// -or-
        /// Parameter value <paramref name="contentType"/> equals <see langword="null"/>.
        /// </exception>
        /// <exception cref="System.ArgumentException">Parameter value <paramref name="contentType"/> is an empty string.</exception>
        /// <exception cref="MVNet.HttpException">Error while working with HTTP protocol.</exception>
        public HttpResponse Put(Uri address, Stream stream, string contentType = "application/octet-stream")
        {
            #region Parameter Check

            if (stream == null)
                throw new ArgumentNullException(nameof(stream));

            if (contentType == null)
                throw new ArgumentNullException(nameof(contentType));

            if (contentType.Length == 0)
                throw new ArgumentNullException(nameof(contentType));

            #endregion

            var content = new StreamContent(stream)
            {
                ContentType = contentType
            };

            return Raw(HttpMethod.Put, address, content);
        }

        /// <summary>
        /// Sends a PUT request to an HTTP server.
        /// </summary>
        /// <param name="address">Internet resource address.</param>
        /// <param name="path">The path to the file whose data will be sent to the HTTP server.</param>
        /// <returns>An object for downloading a response from an HTTP server.</returns>
        /// <exception cref="System.ArgumentNullException">
        /// Parameter value <paramref name="address"/> equals <see langword="null"/>.
        /// -or-
        /// Parameter value <paramref name="path"/> equals <see langword="null"/>.
        /// </exception>
        /// <exception cref="System.ArgumentException">
        /// Parameter value <paramref name="address"/> is an empty string.
        /// -or-
        /// Parameter value <paramref name="path"/> is an empty string.
        /// </exception>
        /// <exception cref="MVNet.HttpException">Error while working with HTTP protocol.</exception>
        public HttpResponse Put(string address, string path)
        {
            #region Parameter Check

            if (path == null)
                throw new ArgumentNullException(nameof(path));

            if (path.Length == 0)
                throw new ArgumentNullException(nameof(path));

            #endregion

            return Raw(HttpMethod.Put, address, new FileContent(path));
        }

        /// <summary>
        /// Sends a PUT request to an HTTP server.
        /// </summary>
        /// <param name="address">Internet resource address.</param>
        /// <param name="path">The path to the file whose data will be sent to the HTTP server.</param>
        /// <returns>An object for downloading a response from an HTTP server.</returns>
        /// <exception cref="System.ArgumentNullException">
        /// Parameter value <paramref name="address"/> equals <see langword="null"/>.
        /// -or-
        /// Parameter value <paramref name="path"/> equals <see langword="null"/>.
        /// </exception>
        /// <exception cref="System.ArgumentException">Parameter value <paramref name="path"/> is an empty string.</exception>
        /// <exception cref="MVNet.HttpException">Error while working with HTTP protocol.</exception>
        public HttpResponse Put(Uri address, string path)
        {
            #region Parameter Check

            if (path == null)
                throw new ArgumentNullException(nameof(path));

            if (path.Length == 0)
                throw new ArgumentNullException(nameof(path));

            #endregion

            return Raw(HttpMethod.Put, address, new FileContent(path));
        }

        /// <summary>
        /// Sends a PUT request to an HTTP server.
        /// </summary>
        /// <param name="address">Internet resource address.</param>
        /// <param name="content">Content sent to the HTTP server.</param>
        /// <returns>An object for downloading a response from an HTTP server.</returns>
        /// <exception cref="System.ArgumentNullException">
        /// Parameter value <paramref name="address"/> equals <see langword="null"/>.
        /// -or-
        /// Parameter value <paramref name="content"/> equals <see langword="null"/>.
        /// </exception>
        /// <exception cref="System.ArgumentException">Parameter value <paramref name="address"/> is an empty string.</exception>
        /// <exception cref="MVNet.HttpException">Error while working with HTTP protocol.</exception>
        public HttpResponse Put(string address, HttpContent content)
        {
            #region Parameter Check

            if (content == null)
                throw new ArgumentNullException(nameof(content));

            #endregion

            return Raw(HttpMethod.Put, address, content);
        }

        /// <summary>
        /// Sends a PUT request to an HTTP server.
        /// </summary>
        /// <param name="address">Internet resource address.</param>
        /// <param name="content">Content sent to the HTTP server.</param>
        /// <returns>An object for downloading a response from an HTTP server.</returns>
        /// <exception cref="System.ArgumentNullException">
        /// Parameter value <paramref name="address"/> equals <see langword="null"/>.
        /// -or-
        /// Parameter value <paramref name="content"/> equals <see langword="null"/>.
        /// </exception>
        /// <exception cref="MVNet.HttpException">Error while working with HTTP protocol.</exception>
        public HttpResponse Put(Uri address, HttpContent content)
        {
            #region Parameter Check

            if (content == null)
                throw new ArgumentNullException(nameof(content));

            #endregion

            return Raw(HttpMethod.Put, address, content);
        }

        #endregion

        #region Delete

        /// <summary>
        /// Sends a DELETE request to an HTTP server.
        /// </summary>
        /// <param name="address">Internet resource address.</param>
        /// <returns>An object for downloading a response from an HTTP server.</returns>
        /// <exception cref="System.ArgumentNullException">Parameter value <paramref name="address"/> equals <see langword="null"/>.</exception>
        /// <exception cref="System.ArgumentException">Parameter value <paramref name="address"/> is an empty string.</exception>
        /// <exception cref="MVNet.HttpException">Error while working with HTTP protocol.</exception>
        public HttpResponse Delete(string address)
        {
            return Raw(HttpMethod.Delete, address);
        }

        /// <summary>
        /// Sends a DELETE request to an HTTP server.
        /// </summary>
        /// <param name="address">Internet resource address.</param>
        /// <returns>An object for downloading a response from an HTTP server.</returns>
        /// <exception cref="System.ArgumentNullException">Parameter value <paramref name="address"/> equals <see langword="null"/>.</exception>
        /// <exception cref="MVNet.HttpException">Error while working with HTTP protocol.</exception>
        public HttpResponse Delete(Uri address)
        {
            return Raw(HttpMethod.Delete, address);
        }

        /// <summary>
        /// Sends a DELETE request to an HTTP server.
        /// </summary>
        /// <param name="address">Internet resource address.</param>
        /// <param name="reqParams">Request parameters sent to the HTTP server.</param>
        /// <returns>An object for downloading a response from an HTTP server.</returns>
        /// <exception cref="System.ArgumentNullException">
        /// Parameter value <paramref name="address"/> equals <see langword="null"/>.
        /// -or-
        /// Parameter value <paramref name="reqParams"/> equals <see langword="null"/>.
        /// </exception>
        /// <exception cref="System.ArgumentException">Parameter value <paramref name="address"/> is an empty string.</exception>
        /// <exception cref="MVNet.HttpException">Error while working with HTTP protocol.</exception>
        public HttpResponse Delete(string address, RequestParams reqParams)
        {
            #region Parameter Check

            if (reqParams == null)
                throw new ArgumentNullException(nameof(reqParams));

            #endregion

            return Raw(HttpMethod.Delete, address, new FormUrlEncodedContent(reqParams));
        }

        /// <summary>
        /// Sends a DELETE request to an HTTP server.
        /// </summary>
        /// <param name="address">Internet resource address.</param>
        /// <param name="reqParams">Request parameters sent to the HTTP server.</param>
        /// <returns>An object for downloading a response from an HTTP server.</returns>
        /// <exception cref="System.ArgumentNullException">
        /// Parameter value <paramref name="address"/> equals <see langword="null"/>.
        /// -or-
        /// Parameter value <paramref name="reqParams"/> equals <see langword="null"/>.
        /// </exception>
        /// <exception cref="MVNet.HttpException">Error while working with HTTP protocol.</exception>
        public HttpResponse Delete(Uri address, RequestParams reqParams)
        {
            #region Parameter Check

            if (reqParams == null)
                throw new ArgumentNullException(nameof(reqParams));

            #endregion

            return Raw(HttpMethod.Delete, address, new FormUrlEncodedContent(reqParams));
        }

        /// <summary>
        /// Sends a DELETE request to an HTTP server.
        /// </summary>
        /// <param name="address">Internet resource address.</param>
        /// <param name="str">The string sent to the HTTP server.</param>
        /// <param name="contentType">The type of data being sent.</param>
        /// <returns>An object for downloading a response from an HTTP server.</returns>
        /// <exception cref="System.ArgumentNullException">
        /// Parameter value <paramref name="address"/> equals <see langword="null"/>.
        /// -or-
        /// Parameter value <paramref name="str"/> equals <see langword="null"/>.
        /// -or-
        /// Parameter value <paramref name="contentType"/> equals <see langword="null"/>.
        /// </exception>
        /// <exception cref="System.ArgumentException">
        /// Parameter value <paramref name="address"/> is an empty string.
        /// -or-
        /// Parameter value <paramref name="str"/> is an empty string.
        /// -or-
        /// Parameter value <paramref name="contentType"/> is an empty string.
        /// </exception>
        /// <exception cref="MVNet.HttpException">Error while working with HTTP protocol.</exception>
        public HttpResponse Delete(string address, string str, string contentType)
        {
            #region Parameter Check

            if (str == null)
                throw new ArgumentNullException(nameof(str));

            if (str.Length == 0)
                throw new ArgumentNullException(nameof(str));

            if (contentType == null)
                throw new ArgumentNullException(nameof(contentType));

            if (contentType.Length == 0)
                throw new ArgumentNullException(nameof(contentType));

            #endregion

            var content = new StringContent(str)
            {
                ContentType = contentType
            };

            return Raw(HttpMethod.Delete, address, content);
        }

        /// <summary>
        /// Sends a DELETE request to an HTTP server.
        /// </summary>
        /// <param name="address">Internet resource address.</param>
        /// <param name="str">The string sent to the HTTP server.</param>
        /// <param name="contentType">The type of data being sent.</param>
        /// <returns>An object for downloading a response from an HTTP server.</returns>
        /// <exception cref="System.ArgumentNullException">
        /// Parameter value <paramref name="address"/> equals <see langword="null"/>.
        /// -or-
        /// Parameter value <paramref name="str"/> equals <see langword="null"/>.
        /// -or-
        /// Parameter value <paramref name="contentType"/> equals <see langword="null"/>.
        /// </exception>
        /// <exception cref="System.ArgumentException">
        /// Parameter value <paramref name="str"/> is an empty string.
        /// -or-
        /// Parameter value <paramref name="contentType"/> is an empty string.
        /// </exception>
        /// <exception cref="MVNet.HttpException">Error while working with HTTP protocol.</exception>
        public HttpResponse Delete(Uri address, string str, string contentType)
        {
            #region Parameter Check

            if (str == null)
                throw new ArgumentNullException(nameof(str));

            if (str.Length == 0)
                throw new ArgumentNullException(nameof(str));

            if (contentType == null)
                throw new ArgumentNullException(nameof(contentType));

            if (contentType.Length == 0)
                throw new ArgumentNullException(nameof(contentType));

            #endregion

            var content = new StringContent(str)
            {
                ContentType = contentType
            };

            return Raw(HttpMethod.Delete, address, content);
        }

        /// <summary>
        /// Sends a DELETE request to an HTTP server.
        /// </summary>
        /// <param name="address">Internet resource address.</param>
        /// <param name="bytes">An array of bytes sent to the HTTP server.</param>
        /// <param name="contentType">The type of data being sent.</param>
        /// <returns>An object for downloading a response from an HTTP server.</returns>
        /// <exception cref="System.ArgumentNullException">
        /// Parameter value <paramref name="address"/> equals <see langword="null"/>.
        /// -or-
        /// Parameter value <paramref name="bytes"/> equals <see langword="null"/>.
        /// -or-
        /// Parameter value <paramref name="contentType"/> equals <see langword="null"/>.
        /// </exception>
        /// <exception cref="System.ArgumentException">
        /// Parameter value <paramref name="address"/> is an empty string.
        /// -or-
        /// Parameter value <paramref name="contentType"/> is an empty string.
        /// </exception>
        /// <exception cref="MVNet.HttpException">Error while working with HTTP protocol.</exception>
        public HttpResponse Delete(string address, byte[] bytes, string contentType = "application/octet-stream")
        {
            #region Parameter Check

            if (bytes == null)
                throw new ArgumentNullException(nameof(bytes));

            if (contentType == null)
                throw new ArgumentNullException(nameof(contentType));

            if (contentType.Length == 0)
                throw new ArgumentNullException(nameof(contentType));

            #endregion

            var content = new BytesContent(bytes)
            {
                ContentType = contentType
            };


            return Raw(HttpMethod.Delete, address, content);
        }

        /// <summary>
        /// Sends a DELETE request to an HTTP server.
        /// </summary>
        /// <param name="address">Internet resource address.</param>
        /// <param name="bytes">An array of bytes sent to the HTTP server.</param>
        /// <param name="contentType">The type of data being sent.</param>
        /// <returns>An object for downloading a response from an HTTP server.</returns>
        /// <exception cref="System.ArgumentNullException">
        /// Parameter value <paramref name="address"/> equals <see langword="null"/>.
        /// -or-
        /// Parameter value <paramref name="bytes"/> equals <see langword="null"/>.
        /// -or-
        /// Parameter value <paramref name="contentType"/> equals <see langword="null"/>.
        /// </exception>
        /// <exception cref="System.ArgumentException">Parameter value <paramref name="contentType"/> is an empty string.</exception>
        /// <exception cref="MVNet.HttpException">Error while working with HTTP protocol.</exception>
        public HttpResponse Delete(Uri address, byte[] bytes, string contentType = "application/octet-stream")
        {
            #region Parameter Check

            if (bytes == null)
                throw new ArgumentNullException(nameof(bytes));

            if (contentType == null)
                throw new ArgumentNullException(nameof(contentType));

            if (contentType.Length == 0)
                throw new ArgumentNullException(nameof(contentType));

            #endregion

            var content = new BytesContent(bytes)
            {
                ContentType = contentType
            };

            return Raw(HttpMethod.Delete, address, content);
        }

        /// <summary>
        /// Sends a DELETE request to an HTTP server.
        /// </summary>
        /// <param name="address">Internet resource address.</param>
        /// <param name="stream">The data stream sent to the HTTP server.</param>
        /// <param name="contentType">The type of data being sent.</param>
        /// <returns>An object for downloading a response from an HTTP server.</returns>
        /// <exception cref="System.ArgumentNullException">
        /// Parameter value <paramref name="address"/> equals <see langword="null"/>.
        /// -or-
        /// Parameter value <paramref name="stream"/> equals <see langword="null"/>.
        /// -or-
        /// Parameter value <paramref name="contentType"/> equals <see langword="null"/>.
        /// </exception>
        /// <exception cref="System.ArgumentException">
        /// Parameter value <paramref name="address"/> is an empty string.
        /// -or-
        /// Parameter value <paramref name="contentType"/> is an empty string.
        /// </exception>
        /// <exception cref="MVNet.HttpException">Error while working with HTTP protocol.</exception>
        public HttpResponse Delete(string address, Stream stream, string contentType = "application/octet-stream")
        {
            #region Parameter Check

            if (stream == null)
                throw new ArgumentNullException(nameof(stream));

            if (contentType == null)
                throw new ArgumentNullException(nameof(contentType));

            if (contentType.Length == 0)
                throw new ArgumentNullException(nameof(contentType));

            #endregion

            var content = new StreamContent(stream)
            {
                ContentType = contentType
            };

            return Raw(HttpMethod.Delete, address, content);
        }

        /// <summary>
        /// Sends a DELETE request to an HTTP server.
        /// </summary>
        /// <param name="address">Internet resource address.</param>
        /// <param name="stream">The data stream sent to the HTTP server.</param>
        /// <param name="contentType">The type of data being sent.</param>
        /// <returns>An object for downloading a response from an HTTP server.</returns>
        /// <exception cref="System.ArgumentNullException">
        /// Parameter value <paramref name="address"/> equals <see langword="null"/>.
        /// -or-
        /// Parameter value <paramref name="stream"/> equals <see langword="null"/>.
        /// -or-
        /// Parameter value <paramref name="contentType"/> equals <see langword="null"/>.
        /// </exception>
        /// <exception cref="System.ArgumentException">Parameter value <paramref name="contentType"/> is an empty string.</exception>
        /// <exception cref="MVNet.HttpException">Error while working with HTTP protocol.</exception>
        public HttpResponse Delete(Uri address, Stream stream, string contentType = "application/octet-stream")
        {
            #region Parameter Check

            if (stream == null)
                throw new ArgumentNullException(nameof(stream));

            if (contentType == null)
                throw new ArgumentNullException(nameof(contentType));

            if (contentType.Length == 0)
                throw new ArgumentNullException(nameof(contentType));

            #endregion

            var content = new StreamContent(stream)
            {
                ContentType = contentType
            };

            return Raw(HttpMethod.Delete, address, content);
        }

        /// <summary>
        /// Sends a DELETE request to an HTTP server.
        /// </summary>
        /// <param name="address">Internet resource address.</param>
        /// <param name="path">The path to the file whose data will be sent to the HTTP server.</param>
        /// <returns>An object for downloading a response from an HTTP server.</returns>
        /// <exception cref="System.ArgumentNullException">
        /// Parameter value <paramref name="address"/> equals <see langword="null"/>.
        /// -or-
        /// Parameter value <paramref name="path"/> equals <see langword="null"/>.
        /// </exception>
        /// <exception cref="System.ArgumentException">
        /// Parameter value <paramref name="address"/> is an empty string.
        /// -or-
        /// Parameter value <paramref name="path"/> is an empty string.
        /// </exception>
        /// <exception cref="MVNet.HttpException">Error while working with HTTP protocol.</exception>
        public HttpResponse Delete(string address, string path)
        {
            #region Parameter Check

            if (path == null)
                throw new ArgumentNullException(nameof(path));

            if (path.Length == 0)
                throw new ArgumentNullException(nameof(path));

            #endregion

            return Raw(HttpMethod.Delete, address, new FileContent(path));
        }

        /// <summary>
        /// Sends a DELETE request to an HTTP server.
        /// </summary>
        /// <param name="address">Internet resource address.</param>
        /// <param name="path">The path to the file whose data will be sent to the HTTP server.</param>
        /// <returns>An object for downloading a response from an HTTP server.</returns>
        /// <exception cref="System.ArgumentNullException">
        /// Parameter value <paramref name="address"/> equals <see langword="null"/>.
        /// -or-
        /// Parameter value <paramref name="path"/> equals <see langword="null"/>.
        /// </exception>
        /// <exception cref="System.ArgumentException">Parameter value <paramref name="path"/> is an empty string.</exception>
        /// <exception cref="MVNet.HttpException">Error while working with HTTP protocol.</exception>
        public HttpResponse Delete(Uri address, string path)
        {
            #region Parameter Check

            if (path == null)
                throw new ArgumentNullException(nameof(path));

            if (path.Length == 0)
                throw new ArgumentNullException(nameof(path));

            #endregion

            return Raw(HttpMethod.Delete, address, new FileContent(path));
        }

        /// <summary>
        /// Sends a DELETE request to an HTTP server.
        /// </summary>
        /// <param name="address">Internet resource address.</param>
        /// <param name="content">Content sent to the HTTP server.</param>
        /// <returns>An object for downloading a response from an HTTP server.</returns>
        /// <exception cref="System.ArgumentNullException">
        /// Parameter value <paramref name="address"/> equals <see langword="null"/>.
        /// -or-
        /// Parameter value <paramref name="content"/> equals <see langword="null"/>.
        /// </exception>
        /// <exception cref="System.ArgumentException">Parameter value <paramref name="address"/> is an empty string.</exception>
        /// <exception cref="MVNet.HttpException">Error while working with HTTP protocol.</exception>
        public HttpResponse Delete(string address, HttpContent content)
        {
            #region Parameter Check

            if (content == null)
                throw new ArgumentNullException(nameof(content));

            #endregion

            return Raw(HttpMethod.Delete, address, content);
        }

        /// <summary>
        /// Sends a DELETE request to an HTTP server.
        /// </summary>
        /// <param name="address">Internet resource address.</param>
        /// <param name="content">Content sent to the HTTP server.</param>
        /// <returns>An object for downloading a response from an HTTP server.</returns>
        /// <exception cref="System.ArgumentNullException">
        /// Parameter value <paramref name="address"/> equals <see langword="null"/>.
        /// -or-
        /// Parameter value <paramref name="content"/> equals <see langword="null"/>.
        /// </exception>
        /// <exception cref="MVNet.HttpException">Error while working with HTTP protocol.</exception>
        public HttpResponse Delete(Uri address, HttpContent content)
        {
            #region Parameter Check

            if (content == null)
                throw new ArgumentNullException(nameof(content));

            #endregion

            return Raw(HttpMethod.Delete, address, content);
        }

        #endregion

        #region Raw

        /// <summary>
        /// Sends a request to an HTTP server.
        /// </summary>
        /// <param name="method">HTTP request method.</param>
        /// <param name="address">Internet resource address.</param>
        /// <param name="content">Content sent to HTTP server or value <see langword="null"/>.</param>
        /// <returns>An object for downloading a response from an HTTP server.</returns>
        /// <exception cref="System.ArgumentNullException">Parameter value <paramref name="address"/> equals <see langword="null"/>.</exception>
        /// <exception cref="System.ArgumentException">Parameter value <paramref name="address"/> is an empty string.</exception>
        /// <exception cref="MVNet.HttpException">Error while working with HTTP protocol.</exception>
        public HttpResponse Raw(HttpMethod method, string address, HttpContent content = null)
        {
            #region Parameter Check

            if (address == null)
                throw new ArgumentNullException(nameof(address));

            if (address.Length == 0)
                throw ExceptionHelper.EmptyString(nameof(address));

            #endregion

            var uri = new Uri(address, UriKind.RelativeOrAbsolute);
            return Raw(method, uri, content);
        }

        /// <summary>
        /// Sends a request to an HTTP server.
        /// </summary>
        /// <param name="method">HTTP request method.</param>
        /// <param name="address">Internet resource address.</param>
        /// <param name="content">Content sent to HTTP server or value <see langword="null"/>.</param>
        /// <returns>An object for downloading a response from an HTTP server.</returns>
        /// <exception cref="System.ArgumentNullException">Parameter value <paramref name="address"/> equals <see langword="null"/>.</exception>
        /// <exception cref="MVNet.HttpException">Error while working with HTTP protocol.</exception>
        public HttpResponse Raw(HttpMethod method, Uri address, HttpContent content = null)
        {
            #region Parameter Check

            if (address == null)
                throw new ArgumentNullException(nameof(address));

            #endregion

            if (!address.IsAbsoluteUri)
                address = GetRequestAddress(BaseAddress, address);

            if (content == null)
            {
                if (_temporaryMultipartContent != null)
                    content = _temporaryMultipartContent;
            }

            try
            {
                return Request(method, address, content);
            }
            finally
            {
                content?.Dispose();

                ClearRequestData(false);
            }
        }

        #endregion

        #region Other Method

        #region Adding temporary request data

        /// <summary>
        /// Adds a temporary HTTP request header. Such a title overrides the title set via the indexer.
        /// </summary>
        /// <param name="name">The name of the HTTP header.</param>
        /// <param name="value">HTTP header value.</param>
        /// <exception cref="System.ArgumentNullException">
        /// Parameter value <paramref name="name"/> equals <see langword="null"/>.
        /// -or-
        /// Parameter value <paramref name="value"/> equals <see langword="null"/>.
        /// </exception>
        /// <exception cref="System.ArgumentException">
        /// Parameter value <paramref name="name"/> is an empty string.
        /// -or-
        /// Parameter value <paramref name="value"/> is an empty string.
        /// -or-
        /// Setting the value of the HTTP header, which must be set using a custom property/method.
        /// </exception>
        /// <remarks>This HTTP header will be erased after the first request.</remarks>
        public HttpRequest AddHeader(string name, string value)
        {
            #region Parameter Check

            if (name == null)
                throw new ArgumentNullException(nameof(name));

            if (name.Length == 0)
                throw ExceptionHelper.EmptyString(nameof(name));

            if (value == null)
                throw new ArgumentNullException(nameof(value));

            if (value.Length == 0 && !AllowEmptyHeaderValues)
                throw ExceptionHelper.EmptyString(nameof(value));

            #endregion

            if (_temporaryHeaders == null)
            {
                _temporaryHeaders = new Dictionary<string, string>();
            }

            _temporaryHeaders[name] = value;

            return this;
        }

        /// <summary>
        /// Adds an "X-Requested-With" header with a value of "XMLHttpRequest".
        /// Applies to AJAX requests.
        /// </summary>
        /// <returns>Will return the same HttpRequest for the call chain (pipeline).</returns>
        public HttpRequest AddXmlHttpRequestHeader()
        {
            return AddHeader("X-Requested-With", "XMLHttpRequest");
        }

        /// <summary>
        /// Adds a temporary HTTP request header. Such a title overrides the title set via the indexer.
        /// </summary>
        /// <param name="header">HTTP header.</param>
        /// <param name="value">HTTP header value.</param>
        /// <exception cref="System.ArgumentNullException">Parameter value <paramref name="value"/> equals <see langword="null"/>.</exception>
        /// <exception cref="System.ArgumentException">
        /// Parameter value <paramref name="value"/> is an empty string.
        /// -or-
        /// Setting the value of the HTTP header, which must be set using a custom property/method.
        /// </exception>
        /// <remarks>This HTTP header will be erased after the first request.</remarks>
        public HttpRequest AddHeader(HttpHeader header, string value)
        {
            AddHeader(Utility.Headers[header], value);

            return this;
        }

        #endregion

        /// <summary>
        /// Closes the connection to the HTTP server.
        /// </summary>
        /// <remarks>Calling this method is equivalent to calling the method <see cref="Dispose()"/>.</remarks>
        // ReSharper disable once UnusedMember.Global
        public void Close()
        {
            Dispose();
        }

        /// <inheritdoc />
        /// <summary>
        /// Releases all resources used by the current instance of the class <see cref="MVNet.HttpRequest" />.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
        }

        /// <summary>
        /// Determines if the specified cookies are contained.
        /// </summary>
        /// <param name="url">resource address</param>
        /// <param name="name">The name of the cookie.</param>
        /// <returns>Value <see langword="true"/>, if the specified cookies are present, otherwise <see langword="false"/>.</returns>
        public bool ContainsCookie(string url, string name)
        {
            return UseCookies && Cookies != null && Cookies.Contains(url, name);
        }

        #region Working with headers

        /// <summary>
        /// Determines whether the specified HTTP header is contained.
        /// </summary>
        /// <param name="headerName">The name of the HTTP header.</param>
        /// <returns>Value <see langword="true"/>, if the specified HTTP header is contained, otherwise <see langword="false"/>.</returns>
        /// <exception cref="System.ArgumentNullException">Parameter value <paramref name="headerName"/> equals <see langword="null"/>.</exception>
        /// <exception cref="System.ArgumentException">Parameter value <paramref name="headerName"/> is an empty string.</exception>
        public bool ContainsHeader(string headerName)
        {
            #region Parameter Check

            if (headerName == null)
                throw new ArgumentNullException(nameof(headerName));

            if (headerName.Length == 0)
                throw ExceptionHelper.EmptyString(nameof(headerName));

            #endregion

            return _permanentHeaders.ContainsKey(headerName);
        }

        /// <summary>
        /// Determines whether the specified HTTP header is contained.
        /// </summary>
        /// <param name="header">HTTP header.</param>
        /// <returns>Value <see langword="true"/>, if the specified HTTP header is contained, otherwise <see langword="false"/>.</returns>
        public bool ContainsHeader(HttpHeader header)
        {
            return ContainsHeader(Utility.Headers[header]);
        }

        /// <summary>
        /// Returns an enumerable collection of HTTP headers.
        /// </summary>
        /// <returns>Collection of HTTP headers.</returns>
        public Dictionary<string, string>.Enumerator EnumerateHeaders()
        {
            return _permanentHeaders.GetEnumerator();
        }

        /// <summary>
        /// Clears all persistent HTTP headers.
        /// </summary>
        public void ClearAllHeaders() => _permanentHeaders.Clear();

        #endregion

        #endregion

        #endregion

        #region Methods (private)

        /// <summary>
        /// Releases the unmanaged (and optionally managed) resources used by the object <see cref="HttpRequest"/>.
        /// </summary>
        /// <param name="disposing">Value <see langword="true"/> allows you to release managed and unmanaged resources; Value <see langword="false"/> releases only unmanaged resources.</param>
        private void Dispose(bool disposing)
        {
            if (!disposing || TcpClient == null)
                return;

            ClientStream?.Flush();
            ClientStream?.Dispose();
            ClientStream = null;

            ClientNetworkStream?.Flush();
            ClientNetworkStream?.Dispose();
            ClientNetworkStream = null;

            TcpClient.Close();
            TcpClient = null;

            _keepAliveRequestCount = 0;
        }

        /// <summary>
        /// Raises an event <see cref="UploadProgressChanged"/>.
        /// </summary>
        /// <param name="e">Event arguments.</param>
        private void OnUploadProgressChanged(UploadProgressChangedEventArgs e)
        {
            var eventHandler = _uploadProgressChangedHandler;

            eventHandler?.Invoke(this, e);
        }

        /// <summary>
        /// Raises an event <see cref="DownloadProgressChanged"/>.
        /// </summary>
        /// <param name="e">Event arguments.</param>
        private void OnDownloadProgressChanged(DownloadProgressChangedEventArgs e)
        {
            var eventHandler = _downloadProgressChangedHandler;

            eventHandler?.Invoke(this, e);
        }

        private void Init()
        {
            KeepTemporaryHeadersOnRedirect = true;
            AcceptEncoding = "gzip,deflate";
            IgnoreInvalidCookie = false;
            IgnoreProtocolErrors = true;

            KeepAlive = true;
            AllowAutoRedirect = true;
            _tempAllowAutoRedirect = AllowAutoRedirect;

            EnableEncodingContent = true;

            SslProtocols = SslProtocols.Tls | SslProtocols.Tls11 | SslProtocols.Tls12 | SslProtocols.Tls13;

            SslCertificateValidatorCallback = new RemoteCertificateValidationCallback((s, c, ch, e) => { return true; });

            Version version = GetVersion();
            UserAgent = $"MVNet({version.Major}.{version.Minor})";

            ClientCertificates = new X509CertificateCollection();
        }

        public Version GetVersion()
        {
            Assembly Reference = typeof(HttpRequest).Assembly;
            return Reference.GetName().Version;
        }

        private static Uri GetRequestAddress(Uri baseAddress, Uri address)
        {
            Uri requestAddress;

            if (baseAddress == null)
            {
                var uriBuilder = new UriBuilder(address.OriginalString);
                requestAddress = uriBuilder.Uri;
            }
            else
                Uri.TryCreate(baseAddress, address, out requestAddress);

            return requestAddress;
        }

        #region Sending a request

        private HttpResponse Request(HttpMethod method, Uri address, HttpContent content)
        {
            HttpResponse response = new HttpResponse(this);
            while (true)
            {
                _method = method;
                _content = content;

                var previousAddress = Address;
                Address = address;

                bool createdNewConnection;
                try
                {
                    createdNewConnection = TryCreateConnectionOrUseExisting(response, address, previousAddress);
                }
                catch (HttpException)
                {
                    if (CanReconnect)
                        return ReconnectAfterFail();

                    throw;
                }

                if (createdNewConnection)
                    _keepAliveRequestCount = 1;
                else
                    _keepAliveRequestCount++;

                #region Sending a request

                try
                {
                    SendRequestData(address, method);
                }
                catch (SecurityException ex)
                {
                    throw NewHttpException(Constants.HttpException_FailedSendRequest, ex, HttpExceptionStatus.SendFailure);
                }
                catch (IOException ex)
                {
                    if (CanReconnect)
                        return ReconnectAfterFail();

                    throw NewHttpException(Constants.HttpException_FailedSendRequest, ex, HttpExceptionStatus.SendFailure);
                }

                #endregion

                #region Loading response headers

                try
                {
                    ReceiveResponseHeaders(response, method);
                }
                catch (HttpException ex)
                {
                    if (CanReconnect)
                        return ReconnectAfterFail();

                    // If the server terminated the persistent connection by returning an empty response, then we try to connect again.
                    // It could terminate the connection because the maximum allowed number of requests has been reached or because the idle time has expired.
                    if (KeepAlive && !_keepAliveReconnected && !createdNewConnection && ex.EmptyMessageBody)
                        return KeepAliveReconnect();

                    throw;
                }

                #endregion

                response.ReconnectCount = _reconnectCount;

                _reconnectCount = 0;
                _keepAliveReconnected = false;
                _whenConnectionIdle = DateTime.Now;

                if (!IgnoreProtocolErrors)
                    CheckStatusCode(response);

                #region Forwarding

                if (AllowAutoRedirect && response.HasRedirect)
                {
                    if (++_redirectionCount > _maximumAutomaticRedirections)
                        throw NewHttpException(Constants.HttpException_LimitRedirections);

                    if (response.HasExternalRedirect)
                        return response;

                    ClearRequestData(true);

                    method = HttpMethod.Get;
                    address = response.RedirectAddress;
                    content = null;
                    continue;
                }

                _redirectionCount = 0;

                #endregion

                response.ReadAsBytes();

                return response;
            }
        }

        private bool TryCreateConnectionOrUseExisting(HttpResponse response, Uri address, Uri previousAddress)
        {
            var proxy = GetProxy();

            bool hasConnection = TcpClient != null;
            bool proxyChanged = !Equals(_currentProxy, proxy);

            bool addressChanged =
                previousAddress == null ||
                previousAddress.Port != address.Port ||
                previousAddress.Host != address.Host ||
                previousAddress.Scheme != address.Scheme;

            // Fix by Igor Vacil'ev
            bool connectionClosedByServer = response.ContainsHeader("Connection") && response["Connection"] == "close";

            // If you need to create a new connection.
            if (hasConnection && !proxyChanged && !addressChanged && !response.HasError &&
                !KeepAliveLimitIsReached(response) && !connectionClosedByServer)
                return false;

            _currentProxy = proxy;

            Dispose();
            CreateConnection(response, address);
            return true;
        }

        private bool KeepAliveLimitIsReached(HttpResponse response)
        {
            if (!KeepAlive)
                return false;

            int maximumKeepAliveRequests = response.MaximumKeepAliveRequests ?? _maximumKeepAliveRequests;

            if (_keepAliveRequestCount >= maximumKeepAliveRequests)
                return true;

            int keepAliveTimeout = response.KeepAliveTimeout ?? _keepAliveTimeout;

            var timeLimit = _whenConnectionIdle.AddMilliseconds(keepAliveTimeout);

            return timeLimit < DateTime.Now;
        }

        private void SendRequestData(Uri uri, HttpMethod method)
        {
            long contentLength = 0L;
            string contentType = string.Empty;

            if (CanContainsRequestBody(method) && _content != null)
            {
                contentType = _content.ContentType;
                contentLength = _content.CalculateContentLength();
            }


            string startingLine = GenerateStartingLine(method);
            string headers = GenerateHeaders(uri, method, contentLength, contentType);

            var startingLineBytes = Encoding.ASCII.GetBytes(startingLine);
            var headersBytes = Encoding.ASCII.GetBytes(headers);

            _bytesSent = 0;
            _totalBytesSent = startingLineBytes.Length + headersBytes.Length + contentLength;

            ClientStream.Write(startingLineBytes, 0, startingLineBytes.Length);
            ClientStream.Write(headersBytes, 0, headersBytes.Length);

            bool hasRequestBody = _content != null && contentLength > 0;

            // Send the request body if it is not present.
            if (hasRequestBody)
                _content.WriteTo(ClientStream);
        }

        private void ReceiveResponseHeaders(HttpResponse response, HttpMethod method)
        {
            _canReportBytesReceived = false;

            _bytesReceived = 0;
            _totalBytesReceived = response.LoadResponse(method, EnableMiddleHeaders);

            _canReportBytesReceived = true;
        }

        private bool CanReconnect => Reconnect && _reconnectCount < _reconnectLimit;

        private HttpResponse ReconnectAfterFail()
        {
            Dispose();
            Thread.Sleep(_reconnectDelay);

            _reconnectCount++;
            return Request(_method, Address, _content);
        }

        private HttpResponse KeepAliveReconnect()
        {
            Dispose();
            _keepAliveReconnected = true;
            return Request(_method, Address, _content);
        }

        private void CheckStatusCode(HttpResponse response)
        {
            int statusCodeNum = (int)response.StatusCode;

            if (statusCodeNum >= 400 && statusCodeNum < 500)
            {
                throw new HttpException(string.Format(Constants.HttpException_ClientError, statusCodeNum), HttpExceptionStatus.ProtocolError, response.StatusCode);
            }

            if (statusCodeNum >= 500)
            {
                throw new HttpException(string.Format(Constants.HttpException_SeverError, statusCodeNum), HttpExceptionStatus.ProtocolError, response.StatusCode);
            }
        }

        private static bool CanContainsRequestBody(HttpMethod method)
        {
            return
                method == HttpMethod.Post ||
                method == HttpMethod.Put ||
                method == HttpMethod.Patch ||
                method == HttpMethod.Delete;
        }

        #endregion

        #region Create a connection

        private ProxyClient GetProxy()
        {
            if (!DisableProxyForLocalAddress)
                return Proxy ?? GlobalProxy;

            try
            {
                var checkIp = IPAddress.Parse("127.0.0.1");
                var ips = Dns.GetHostAddresses(Address.Host);

                foreach (var ip in ips)
                {
                    if (ip.Equals(checkIp))
                        return null;
                }
            }
            catch (Exception ex)
            {
                if (ex is SocketException || ex is ArgumentException)
                    throw NewHttpException(Constants.HttpException_FailedGetHostAddresses, ex);

                throw;
            }

            return Proxy ?? GlobalProxy;
        }

        private TcpClient CreateTcpConnection(string host, int port)
        {
            TcpClient tcpClient;

            if (_currentProxy == null)
            {
                #region Create a connection

                tcpClient = new TcpClient();

                Exception connectException = null;
                var connectDoneEvent = new ManualResetEventSlim();

                try
                {
                    tcpClient.BeginConnect(host, port, ar => {
                        try
                        {
                            tcpClient.EndConnect(ar);
                        }
                        catch (Exception ex)
                        {
                            connectException = ex;
                        }

                        connectDoneEvent.Set();
                    }, tcpClient);
                }

                #region Catch's

                catch (Exception ex)
                {
                    tcpClient.Close();

                    if (ex is SocketException || ex is SecurityException)
                    {
                        throw NewHttpException(Constants.HttpException_FailedConnect, ex,
                            HttpExceptionStatus.ConnectFailure);
                    }

                    throw;
                }

                #endregion

                if (!connectDoneEvent.Wait(_connectTimeout))
                {
                    tcpClient.Close();
                    throw NewHttpException(Constants.HttpException_ConnectTimeout, null,
                        HttpExceptionStatus.ConnectFailure);
                }

                if (connectException != null)
                {
                    tcpClient.Close();

                    if (connectException is SocketException)
                    {
                        throw NewHttpException(Constants.HttpException_FailedConnect, connectException, HttpExceptionStatus.ConnectFailure);
                    }

                    throw connectException;
                }

                if (!tcpClient.Connected)
                {
                    tcpClient.Close();
                    throw NewHttpException(Constants.HttpException_FailedConnect, null, HttpExceptionStatus.ConnectFailure);
                }

                #endregion

                tcpClient.SendTimeout = _readWriteTimeout;
                tcpClient.ReceiveTimeout = _readWriteTimeout;
            }
            else
            {
                try
                {
                    tcpClient = _currentProxy.CreateConnection(host, port);
                }
                catch (ProxyException ex)
                {
                    throw NewHttpException(Constants.HttpException_FailedConnect, ex,
                        HttpExceptionStatus.ConnectFailure);
                }
            }

            return tcpClient;
        }

        private void CreateConnection(HttpResponse response, Uri address)
        {
            TcpClient = CreateTcpConnection(address.Host, address.Port);
            ClientNetworkStream = TcpClient.GetStream();

            // If a secure connection is required.
            if (address.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var sslStream = new SslStream(ClientNetworkStream, false, SslCertificateValidatorCallback, LocalCertificateSelectionCallback);

                    var sslOptions = new SslClientAuthenticationOptions
                    {
                        TargetHost = address.Host,
                        EnabledSslProtocols = SslProtocols,
                        AllowRenegotiation = true,
                        ClientCertificates = ClientCertificates
                    };

                    ClientStream = sslStream;
                    sslStream.AuthenticateAsClient(sslOptions);

                    response.CipherAlgorithm = sslStream.CipherAlgorithm;
                    response.HashAlgorithm = sslStream.HashAlgorithm;
                    response.TlsCipher = sslStream.NegotiatedCipherSuite;
                    response.CipherStrength = sslStream.CipherStrength;
                    response.SslProtocol = sslStream.SslProtocol;
                    response.LocalCertificate = sslStream.LocalCertificate;
                    response.RemoteCertificate = sslStream.RemoteCertificate;

                }
                catch (Exception ex)
                {
                    if (ex is IOException || ex is AuthenticationException)
                    {
                        throw NewHttpException(Constants.HttpException_FailedSslConnect, ex, HttpExceptionStatus.ConnectFailure);
                    }

                    throw;
                }
            }
            else
            {
                ClientStream = ClientNetworkStream;
            }

            if (_uploadProgressChangedHandler == null && _downloadProgressChangedHandler == null)
                return;

            var httpWrapperStream = new HttpWrapperStream(
                ClientStream, TcpClient.SendBufferSize);

            if (_uploadProgressChangedHandler != null)
                httpWrapperStream.BytesWriteCallback = ReportBytesSent;

            if (_downloadProgressChangedHandler != null)
                httpWrapperStream.BytesReadCallback = ReportBytesReceived;

            ClientStream = httpWrapperStream;
        }

        #endregion

        #region Formation of request data

        private string GenerateStartingLine(HttpMethod method)
        {
            // Fix by Igor Vacil'ev: sometimes proxies returns 404 when used full path.
            bool hasHttpProxyWithAbsoluteUriInStartingLine =
                _currentProxy != null &&
                _currentProxy.Type == ProxyType.HTTP &&
                _currentProxy.AbsoluteUriInStartingLine;

            string query = hasHttpProxyWithAbsoluteUriInStartingLine
                ? Address.AbsoluteUri
                : Address.PathAndQuery;

            return $"{method} {query} HTTP/{ProtocolVersion}\r\n";
        }


        //private string GenerateStartingLine(HttpMethod method) => $"{method} {Address.PathAndQuery} HTTP/{ProtocolVersion}\r\n";

        // There are 3 types of headers that can be overlapped by others. Here is the order in which they are installed:
        // - headers that are set through special properties, or automatically
        // - headers that are set through the indexer
        // - temporary headers that are set via the AddHeader method
        private string GenerateHeaders(Uri uri, HttpMethod method, long contentLength = 0, string contentType = null)
        {
            var headers = GenerateCommonHeaders(method, contentLength, contentType);

            MergeHeaders(headers, _permanentHeaders);

            if (_temporaryHeaders != null && _temporaryHeaders.Count > 0)
                MergeHeaders(headers, _temporaryHeaders);

            // Disabled cookies
            if (!UseCookies)
                return ToHeadersString(headers);

            // Cookies isn't set now
            if (Cookies == null)
            {
                Cookies = new CookieStorage(ignoreInvalidCookie: IgnoreInvalidCookie);
                return ToHeadersString(headers);
            }

            // No Cookies or cookies is set via direct header
            if (Cookies.Count == 0 || headers.ContainsKey("Cookie"))
                return ToHeadersString(headers);

            // Cookies from storage
            string cookies = Cookies.GetCookieHeader(uri);
            if (!string.IsNullOrEmpty(cookies))
                headers["Cookie"] = cookies;

            return ToHeadersString(headers);
        }

        private Dictionary<string, string> GenerateCommonHeaders(HttpMethod method, long contentLength = 0, string contentType = null)
        {
            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Host"] = Address.IsDefaultPort ? Address.Host : $"{Address.Host}:{Address.Port}"
            };

            #region Connection and Authorization

            HttpProxyClient httpProxy = null;

            if (_currentProxy != null && _currentProxy.Type == ProxyType.HTTP)
                httpProxy = _currentProxy as HttpProxyClient;

            if (httpProxy != null)
            {
                headers["Proxy-Connection"] = KeepAlive ? "keep-alive" : "close";

                if (!string.IsNullOrEmpty(httpProxy.Username) ||
                    !string.IsNullOrEmpty(httpProxy.Password))
                {
                    headers["Proxy-Authorization"] = GetProxyAuthorizationHeader(httpProxy);
                }
            }
            else
                headers["Connection"] = KeepAlive ? "keep-alive" : "close";

            if (!string.IsNullOrEmpty(Username) || !string.IsNullOrEmpty(Password))
                headers["Authorization"] = GetAuthorizationHeader();

            #endregion

            #region Content

            if (EnableEncodingContent)
                headers["Accept-Encoding"] = AcceptEncoding;

            if (Culture != null)
                headers["Accept-Language"] = GetLanguageHeader();

            if (CharacterSet != null)
                headers["Accept-Charset"] = GetCharsetHeader();

            if (!CanContainsRequestBody(method))
                return headers;

            if (contentLength > 0)
                headers["Content-Type"] = contentType;

            headers["Content-Length"] = contentLength.ToString();

            #endregion

            return headers;
        }

        #region Working with headers

        private string GetAuthorizationHeader()
        {
            string data = Convert.ToBase64String(Encoding.UTF8.GetBytes(
                $"{Username}:{Password}"));

            return $"Basic {data}";
        }

        private static string GetProxyAuthorizationHeader(ProxyClient httpProxy)
        {
            string data = Convert.ToBase64String(Encoding.UTF8.GetBytes(
                $"{httpProxy.Username}:{httpProxy.Password}"));

            return $"Basic {data}";
        }

        private string GetLanguageHeader()
        {
            string cultureName = Culture?.Name ?? CultureInfo.CurrentCulture.Name;

            return cultureName.StartsWith("en")
                ? cultureName
                : $"{cultureName},{cultureName.Substring(0, 2)};q=0.8,en-US;q=0.6,en;q=0.4";
        }

        private string GetCharsetHeader()
        {
            if (Equals(CharacterSet, Encoding.UTF8))
                return "utf-8;q=0.7,*;q=0.3";

            string charsetName = CharacterSet?.WebName ?? Encoding.Default.WebName;

            return $"{charsetName},utf-8;q=0.7,*;q=0.3";
        }

        private static void MergeHeaders(IDictionary<string, string> destination, Dictionary<string, string> source)
        {
            foreach (var sourceItem in source)
                destination[sourceItem.Key] = sourceItem.Value;
        }

        #endregion

        private string ToHeadersString(Dictionary<string, string> headers)
        {
            var headersBuilder = new StringBuilder();
            foreach (var header in headers)
            {
                if (header.Key != "Cookie" || CookieSingleHeader)
                {
                    headersBuilder.AppendFormat("{0}: {1}\r\n", header.Key, header.Value);
                    continue;
                }

                // Каждую Cookie в отдельный заголовок
                var cookies = header.Value.Split(new[] { "; " }, StringSplitOptions.None);
                foreach (string cookie in cookies)
                    headersBuilder.AppendFormat("Cookie: {0}\r\n", cookie);
            }

            headersBuilder.AppendLine();
            return headersBuilder.ToString();
        }

        #endregion

        // Reports how many bytes were sent to the HTTP server.
        private void ReportBytesSent(int bytesSent)
        {
            _bytesSent += bytesSent;

            OnUploadProgressChanged(new UploadProgressChangedEventArgs(_bytesSent, _totalBytesSent));
        }

        // Reports how many bytes were received from the HTTP server.
        private void ReportBytesReceived(int bytesReceived)
        {
            _bytesReceived += bytesReceived;

            if (_canReportBytesReceived)
            {
                OnDownloadProgressChanged(new DownloadProgressChangedEventArgs(_bytesReceived, _totalBytesReceived));
            }
        }

        private void ClearRequestData(bool redirect)
        {
            _content = null;

            _temporaryMultipartContent = null;

            if (!redirect || !KeepTemporaryHeadersOnRedirect)
                _temporaryHeaders = null;
        }

        private HttpException NewHttpException(string message, Exception innerException = null, HttpExceptionStatus status = HttpExceptionStatus.Other)
        {
            return new HttpException(string.Format(message, Address.Host), status, 0, innerException);
        }

        #endregion
    }
}
