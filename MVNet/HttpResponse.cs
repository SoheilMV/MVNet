using System.IO.Compression;
using System.Net;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;

namespace MVNet
{
    /// <summary>
    /// Represents a class that is used to download a response from an HTTP server.
    /// </summary>
    public class HttpResponse
    {
        #region Classes (private)

        // A wrapper for an array of bytes.
        // Specifies the actual number of bytes contained in the array.
        private sealed class BytesWrapper
        {
            public int Length { get; set; }

            public byte[] Value { get; set; }
        }

        // This class is used to load initial data.
        // But it is also used to load the message body, more precisely, the rest of the data obtained when loading the initial data is simply unloaded from it.
        private sealed class ReceiverHelper
        {
            private const int InitialLineSize = 1000;


            #region Fields (Private)

            private Stream _stream;

            private readonly byte[] _buffer;
            private readonly int _bufferSize;

            private int _linePosition;
            private byte[] _lineBuffer = new byte[InitialLineSize];

            #endregion


            #region Properties (public)

            public bool HasData => Length - Position != 0;

            private int Length { get; set; }

            public int Position { get; private set; }

            #endregion


            public ReceiverHelper(int bufferSize)
            {
                _bufferSize = bufferSize;
                _buffer = new byte[_bufferSize];
            }


            #region Methods (public)

            public void Init(Stream stream)
            {
                _stream = stream;
                _linePosition = 0;

                Length = 0;
                Position = 0;
            }

            public string ReadLine()
            {
                _linePosition = 0;

                while (true)
                {
                    if (Position == Length)
                    {
                        Position = 0;
                        Length = _stream.Read(_buffer, 0, _bufferSize);

                        if (Length == 0)
                            break;
                    }

                    byte b = _buffer[Position++];

                    _lineBuffer[_linePosition++] = b;

                    // If the character '\n' is read.
                    if (b == 10)
                        break;

                    // If the maximum line buffer size limit has not been reached.
                    if (_linePosition != _lineBuffer.Length)
                        continue;

                    // Double the line buffer size.
                    var newLineBuffer = new byte[_lineBuffer.Length * 2];

                    _lineBuffer.CopyTo(newLineBuffer, 0);
                    _lineBuffer = newLineBuffer;
                }

                return Encoding.ASCII.GetString(_lineBuffer, 0, _linePosition);
            }

            public int Read(byte[] buffer, int index, int length)
            {
                int curLength = Length - Position;

                if (curLength > length)
                    curLength = length;

                Array.Copy(_buffer, Position, buffer, index, curLength);

                Position += curLength;

                return curLength;
            }

            #endregion
        }

        // This class is used when loading compressed data.
        // It allows you to determine the exact number of bytes read (compressed data).
        // This is necessary because streams for reading compressed data report the number of bytes of already converted data.
        private sealed class ZipWrapperStream : Stream
        {
            #region Fields (Private)

            private readonly Stream _baseStream;
            private readonly ReceiverHelper _receiverHelper;

            #endregion


            #region Properties (public)

            private int BytesRead { get; set; }

            public int TotalBytesRead { get; set; }

            public int LimitBytesRead { private get; set; }

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


            public ZipWrapperStream(Stream baseStream, ReceiverHelper receiverHelper)
            {
                _baseStream = baseStream;
                _receiverHelper = receiverHelper;
            }


            #region Methods (public)

            public override void Flush() => _baseStream.Flush();

            public override void SetLength(long value) => _baseStream.SetLength(value);

            public override long Seek(long offset, SeekOrigin origin)
            {
                return _baseStream.Seek(offset, origin);
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                // If a limit is set on the number of bytes read.
                if (LimitBytesRead != 0)
                {
                    int length = LimitBytesRead - TotalBytesRead;

                    // If the limit is reached.
                    if (length == 0)
                        return 0;

                    if (length > buffer.Length)
                        length = buffer.Length;

                    BytesRead = _receiverHelper.HasData
                        ? _receiverHelper.Read(buffer, offset, length)
                        : _baseStream.Read(buffer, offset, length);
                }
                else
                {
                    BytesRead = _receiverHelper.HasData
                        ? _receiverHelper.Read(buffer, offset, count)
                        : _baseStream.Read(buffer, offset, count);
                }

                TotalBytesRead += BytesRead;

                return BytesRead;
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
                _baseStream.Write(buffer, offset, count);
            }

            #endregion
        }

        #endregion


        #region Static fields (private)

        private static readonly byte[] OpenHtmlSignature = Encoding.ASCII.GetBytes("<html");
        private static readonly byte[] CloseHtmlSignature = Encoding.ASCII.GetBytes("</html>");

        private static readonly Regex KeepAliveTimeoutRegex = new Regex(@"timeout(|\s+)=(|\s+)(?<value>\d+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex KeepAliveMaxRegex = new Regex(@"max(|\s+)=(|\s+)(?<value>\d+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex ContentCharsetRegex = new Regex(@"charset(|\s+)=(|\s+)(?<value>[a-z,0-9,-]+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        #endregion


        #region Fields (private)

        private readonly HttpRequest _request;
        private ReceiverHelper _receiverHelper;

        private readonly Dictionary<string, string> _headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // Lazy redirect headers
        public Dictionary<string, string> MiddleHeaders => _middleHeaders ?? (_middleHeaders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));

        private Dictionary<string, string> _middleHeaders;

        private byte[] _messageBody;

        #endregion


        #region Properties (public)

        public CipherAlgorithmType CipherAlgorithm { get; internal set; }
        public HashAlgorithmType HashAlgorithm { get; internal set; }
        public TlsCipherSuite TlsCipher { get; internal set; }
        public int CipherStrength { get; internal set; }
        public SslProtocols SslProtocol { get; internal set; }
        public X509Certificate? RemoteCertificate { get; internal set; }
        public X509Certificate? LocalCertificate { get; internal set; }

        /// <summary>
        /// Gets a value indicating whether an error occurred while receiving a response from the HTTP server.
        /// </summary>
        public bool HasError { get; private set; }

        /// <summary>
        /// Gets a value indicating whether the message body has been loaded.
        /// </summary>
        public bool MessageBodyLoaded { get; private set; }

        /// <summary>
        /// Returns a value indicating whether the request was successful (response code = 200 OK).
        /// </summary>
        public bool IsOK => StatusCode == HttpStatusCode.OK;

        /// <summary>
        /// Gets a value indicating whether there is a redirect.
        /// </summary>
        public bool HasRedirect
        {
            get
            {
                int numStatusCode = (int)StatusCode;

                return numStatusCode >= 300 && numStatusCode < 400
                    || _headers.ContainsKey("Location")
                    || _headers.ContainsKey("Redirect-Location");
            }
        }


        /// <summary>
        /// Gets a value indicating whether the redirect was to a protocol other than HTTP or HTTPS.
        /// </summary>
        public bool HasExternalRedirect =>
            HasRedirect && RedirectAddress != null &&
            !RedirectAddress.Scheme.Equals("http", StringComparison.InvariantCultureIgnoreCase) &&
            !RedirectAddress.Scheme.Equals("https", StringComparison.InvariantCultureIgnoreCase);

        /// <summary>
        /// Returns the number of reconnect attempts.
        /// </summary>
        public int ReconnectCount { get; internal set; }

        #region Basic data

        /// <summary>
        /// Returns the URI of the Internet resource that actually answered the request.
        /// </summary>
        public Uri Address { get; private set; }

        /// <summary>
        /// Returns the HTTP method used to receive the response.
        /// </summary>
        public HttpMethod Method { get; private set; }

        /// <summary>
        /// Returns the HTTP protocol version used in the response.
        /// </summary>
        public Version ProtocolVersion { get; private set; }

        /// <summary>
        /// Returns the response status code.
        /// </summary>
        public HttpStatusCode StatusCode { get; private set; }

        /// <summary>
        /// Returns the redirect address.
        /// </summary>
        /// <returns>Forwarding address, otherwise <see langword="null"/>.</returns>
        public Uri RedirectAddress { get; private set; }

        #endregion

        #region HTTP headers

        /// <summary>
        /// Returns the encoding of the message body.
        /// </summary>
        /// <value>The encoding of the message body, if the corresponding header is specified, otherwise the value specified in <see cref="HttpRequest"/>. If it is not set, then the value <see cref="System.Text.Encoding.Default"/>.</value>
        public Encoding CharacterSet { get; private set; }

        /// <summary>
        /// Returns the length of the message body.
        /// </summary>
        /// <value>The length of the message body if an appropriate header is specified, otherwise -1.</value>
        public long ContentLength { get; private set; }

        /// <summary>
        /// Returns the content type of the response.
        /// </summary>
        /// <value>The content type of the response, if an appropriate header is specified, otherwise an empty string.</value>
        public string ContentType { get; private set; }

        /// <summary>
        /// Returns the value of the 'Location' HTTP header.
        /// </summary>
        /// <returns>The value of the header, if such a header is specified, otherwise the empty string.</returns>
        public string Location => this["Location"];

        /// <summary>
        /// Returns the cookie generated as a result of the request, or set in <see cref="HttpRequest"/>.
        /// </summary>
        /// <remarks>If cookies have been set in <see cref="HttpRequest"/> and property value <see cref="CookieStorage.IsLocked"/> equals <see langword="true"/>, then new cookies will be created.</remarks>
        public CookieStorage Cookies { get; private set; }

        /// <summary>
        /// Returns the idle time of a persistent connection in milliseconds.
        /// </summary>
        /// <value>Default value - <see langword="null"/>.</value>
        public int? KeepAliveTimeout { get; private set; }

        /// <summary>
        /// Returns the maximum allowed number of requests for a single connection.
        /// </summary>
        /// <value>Default value - <see langword="null"/>.</value>
        public int? MaximumKeepAliveRequests { get; private set; }

        #endregion

        #endregion


        #region Indexers (public)

        /// <summary>
        /// Returns the HTTP header value.
        /// </summary>
        /// <param name="headerName">The name of the HTTP header.</param>
        /// <value>The value of the HTTP header, if one is set, otherwise the empty string.</value>
        /// <exception cref="System.ArgumentNullException">Parameter value <paramref name="headerName"/> equals <see langword="null"/>.</exception>
        /// <exception cref="System.ArgumentException">Parameter value <paramref name="headerName"/> is an empty string.</exception>
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

                if (!_headers.TryGetValue(headerName, out string value))
                    value = string.Empty;

                return value;
            }
        }

        /// <summary>
        /// Returns the HTTP header value.
        /// </summary>
        /// <param name="header">HTTP header.</param>
        /// <value>The value of the HTTP header, if one is set, otherwise the empty string.</value>
        public string this[HttpHeader header] => this[Utility.Headers[header]];

        #endregion


        internal HttpResponse(HttpRequest request)
        {
            _request = request;

            ContentLength = -1;
            ContentType = string.Empty;
        }


        #region Methods (public)

        /// <summary>
        /// Loads the message body and returns it as an array of bytes.
        /// </summary>
        /// <returns>If there is no message body, or it has already been downloaded, an empty byte array will be returned.</returns>
        /// <exception cref="System.InvalidOperationException">Calling a method from an erroneous response.</exception>
        /// <exception cref="HttpException">Error while working with HTTP protocol.</exception>
        public byte[] ReadAsBytes()
        {
            #region Status check

            if (HasError)
            {
                throw new InvalidOperationException(Constants.InvalidOperationException_HttpResponse_HasError);
            }

            #endregion

            if (!MessageBodyLoaded)
            {
                using (var memoryStream = new MemoryStream())
                {
                    memoryStream.SetLength(ContentLength == -1 ? 0 : ContentLength);

                    try
                    {
                        var source = GetMessageBodySource();

                        foreach (var bytes in source)
                            memoryStream.Write(bytes.Value, 0, bytes.Length);
                    }
                    catch (Exception ex)
                    {
                        HasError = true;

                        if (ex is IOException || ex is InvalidOperationException)
                            throw NewHttpException(Constants.HttpException_FailedReceiveMessageBody, ex);

                        throw;
                    }

                    if (ConnectionClosed())
                        _request?.Dispose();

                    MessageBodyLoaded = true;

                    _messageBody = memoryStream.ToArray();
                }
            }

            return _messageBody;
        }

        /// <summary>
        /// Loads the message body and returns it as a string.
        /// </summary>
        /// <returns>If there is no message body, or it has already been loaded, an empty string will be returned.</returns>
        /// <exception cref="System.InvalidOperationException">Calling a method from an erroneous response.</exception>
        /// <exception cref="HttpException">Error while working with HTTP protocol.</exception>
        public override string ToString()
        {
            #region Status check

            if (HasError)
                throw new InvalidOperationException(Constants.InvalidOperationException_HttpResponse_HasError);

            #endregion

            return CharacterSet.GetString(_messageBody);
        }

        public string ReadAsString()
        {
            #region Status check

            if (HasError)
                throw new InvalidOperationException(Constants.InvalidOperationException_HttpResponse_HasError);

            #endregion

            return CharacterSet.GetString(_messageBody);
        }

        /// <summary>
        /// Downloads the message body and saves it to a new file at the specified path. If the file already exists, it will be overwritten.
        /// </summary>
        /// <param name="path">The path to the file where the body of the message will be saved.</param>
        /// <exception cref="System.InvalidOperationException">Calling a method from an erroneous response.</exception>
        /// <exception cref="System.ArgumentNullException">Parameter value <paramref name="path"/> equals <see langword="null"/>.</exception>
        /// <exception cref="System.ArgumentException">Parameter value <paramref name="path"/> is an empty string, contains only spaces, or contains invalid characters.</exception>
        /// <exception cref="System.IO.PathTooLongException">The specified path, file name, or both exceeds the maximum length allowed by the system. For example, for Windows-based platforms, paths cannot exceed 248 characters, and file names cannot exceed 260 characters.</exception>
        /// <exception cref="System.IO.FileNotFoundException">Parameter value <paramref name="path"/> points to a non-existent file.</exception>
        /// <exception cref="System.IO.DirectoryNotFoundException">Parameter value <paramref name="path"/> points to an invalid path.</exception>
        /// <exception cref="System.IO.IOException">An I/O error occurred while opening the file.</exception>
        /// <exception cref="System.Security.SecurityException">The caller does not have the required permission.</exception>
        /// <exception cref="System.UnauthorizedAccessException">
        /// The file read operation is not supported on the current platform.
        /// -or-
        /// Parameter value <paramref name="path"/> defines a directory.
        /// -or-
        /// The caller does not have the required permission.
        /// </exception>
        /// <exception cref="HttpException">Error while working with HTTP protocol.</exception>
        public void Write(string path)
        {
            #region Status check

            if (HasError)
            {
                throw new InvalidOperationException(Constants.InvalidOperationException_HttpResponse_HasError);
            }

            #endregion

            #region Parameter Check

            if (path == null)
                throw new ArgumentNullException(nameof(path));

            #endregion

            if (!MessageBodyLoaded)
                ReadAsBytes();

            try
            {
                using (var fileStream = new FileStream(path, FileMode.Create))
                {
                    fileStream.Write(_messageBody, 0, _messageBody.Length);
                }
            }
            #region Catch's

            catch (ArgumentException ex)
            {
                throw ExceptionHelper.WrongPath(nameof(path), ex);
            }
            catch (NotSupportedException ex)
            {
                throw ExceptionHelper.WrongPath(nameof(path), ex);
            }
            catch (Exception ex)
            {
                HasError = true;

                if (ex is IOException || ex is InvalidOperationException)
                    throw NewHttpException(Constants.HttpException_FailedReceiveMessageBody, ex);

                throw;
            }

            #endregion
        }

        /// <summary>
        /// Loads the message body and returns it as a stream of bytes from memory.
        /// </summary>
        /// <returns>If the message body is missing, or it has already been loaded, then the value will be returned. <see langword="null"/>.</returns>
        /// <exception cref="System.InvalidOperationException">Calling a method from an erroneous response.</exception>
        /// <exception cref="HttpException">Error while working with HTTP protocol.</exception>
        public Stream ReadAsStream()
        {
            #region Status check

            if (HasError)
            {
                throw new InvalidOperationException(Constants.InvalidOperationException_HttpResponse_HasError);
            }

            #endregion

            MemoryStream memoryStream = new MemoryStream(_messageBody);
            memoryStream.Position = 0;
            return memoryStream;
        }

        /// <summary>
        /// Skips the body of the message. This method should be called if no message body is required.
        /// </summary>
        /// 
        /// <exception cref="System.InvalidOperationException">Calling a method from an erroneous response.</exception>
        /// <exception cref="HttpException">Error while working with HTTP protocol.</exception>
        public void None()
        {
            #region Status check

            if (HasError)
            {
                throw new InvalidOperationException(Constants.InvalidOperationException_HttpResponse_HasError);
            }

            #endregion

            if (MessageBodyLoaded)
                return;

            if (ConnectionClosed())
                _request.Dispose();
            else
            {
                try
                {
                    var source = GetMessageBodySource();

                    foreach (var unused in source) { }
                }
                catch (Exception ex)
                {
                    HasError = true;

                    if (ex is IOException || ex is InvalidOperationException)
                        throw NewHttpException(Constants.HttpException_FailedReceiveMessageBody, ex);

                    throw;
                }
            }

            MessageBodyLoaded = true;
        }

        #region Working with cookies

        /// <summary>
        /// Determines if the specified cookie is contained at the specified web address.
        /// </summary>
        /// <param name="url">Resource address.</param>
        /// <param name="name">The name of the cookie.</param>
        /// <returns>Value <see langword="true"/>, if the specified cookies are present, otherwise <see langword="false"/>.</returns>
        public bool ContainsCookie(string url, string name)
        {
            return Cookies != null && Cookies.Contains(url, name);
        }

        /// <inheritdoc cref="ContainsCookie(string,string)"/>
        /// <param name="uri">Cookie address</param>
        public bool ContainsCookie(Uri uri, string name)
        {
            return Cookies != null && Cookies.Contains(uri, name);
        }

        /// <inheritdoc cref="ContainsCookie(string,string)"/>
        /// <summary>
        /// Determines if the specified cookie is contained at the address in the response.
        /// </summary>
        public bool ContainsCookie(string name)
        {
            return Cookies != null && Cookies.Contains(HasRedirect && !HasExternalRedirect ? RedirectAddress : Address, name);
        }

        #endregion

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

            return _headers.ContainsKey(headerName);
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
            return _headers.GetEnumerator();
        }

        #endregion

        #endregion


        // Loads the response and returns the size of the response in bytes.
        internal long LoadResponse(HttpMethod method, bool trackMiddleHeaders)
        {
            Method = method;
            Address = _request.Address;

            HasError = false;
            MessageBodyLoaded = false;
            KeepAliveTimeout = null;
            MaximumKeepAliveRequests = null;

            if (trackMiddleHeaders && _headers.Count > 0)
            {
                foreach (string key in _headers.Keys)
                    MiddleHeaders[key] = _headers[key];
            }
            _headers.Clear();

            if (_request.UseCookies)
            {
                Cookies = _request.Cookies != null && !_request.Cookies.IsLocked
                    ? _request.Cookies
                    : new CookieStorage(ignoreInvalidCookie: _request.IgnoreInvalidCookie);
            }

            if (_receiverHelper == null)
                _receiverHelper = new ReceiverHelper(_request.TcpClient.ReceiveBufferSize);

            _receiverHelper.Init(_request.ClientStream);

            try
            {
                ReceiveStartingLine();
                ReceiveHeaders();

                RedirectAddress = GetLocation();
                CharacterSet = GetCharacterSet();
                ContentLength = GetContentLength();
                ContentType = GetContentType();

                KeepAliveTimeout = GetKeepAliveTimeout();
                MaximumKeepAliveRequests = GetKeepAliveMax();
            }
            catch (Exception ex)
            {
                HasError = true;

                if (ex is IOException)
                    throw NewHttpException(Constants.HttpException_FailedReceiveResponse, ex);

                throw;
            }

            // If a response came without a message body.
            if (ContentLength == 0 || Method == HttpMethod.Head || StatusCode == HttpStatusCode.Continue || StatusCode == HttpStatusCode.NoContent || StatusCode == HttpStatusCode.NotModified)
            {
                _messageBody = new byte[0];
                MessageBodyLoaded = true;
            }

            long responseSize = _receiverHelper.Position;

            if (ContentLength > 0)
                responseSize += ContentLength;

            return responseSize;
        }


        #region Methods (private)

        #region Loading initial data

        private void ReceiveStartingLine()
        {
            string startingLine;

            while (true)
            {
                startingLine = _receiverHelper.ReadLine();

                if (startingLine.Length == 0)
                {
                    var exception = NewHttpException(Constants.HttpException_ReceivedEmptyResponse);
                    exception.EmptyMessageBody = true;

                    throw exception;
                }
                if (startingLine != Utility.NewLine)
                    break;
            }

            string version = startingLine.Substring("HTTP/", " ");
            string statusCode = startingLine.Substring(" ", " ");

            // If the server does not return a Reason Phrase
            if (string.IsNullOrEmpty(statusCode))
                statusCode = startingLine.Substring(" ", Utility.NewLine);

            if (string.IsNullOrEmpty(version) || string.IsNullOrEmpty(statusCode))
                throw NewHttpException(Constants.HttpException_ReceivedEmptyResponse);

            ProtocolVersion = Version.Parse(version);

            StatusCode = (HttpStatusCode)Enum.Parse(typeof(HttpStatusCode), statusCode);
        }

        private void ReceiveHeaders()
        {
            while (true)
            {
                string header = _receiverHelper.ReadLine();

                // If the end of headers is reached.
                if (header == Utility.NewLine)
                    return;

                // We are looking for a position between the name and value of the header.
                int separatorPos = header.IndexOf(':');

                if (separatorPos == -1)
                {
                    string message = string.Format(Constants.HttpException_WrongHeader, header, Address.Host);

                    throw NewHttpException(message);
                }

                string headerName = header.Substring(0, separatorPos);
                string headerValue = header.Substring(separatorPos + 1).Trim(' ', '\t', '\r', '\n');

                if (headerName.Equals("Set-Cookie", StringComparison.OrdinalIgnoreCase))
                    ParseCookieFromHeader(headerValue);
                else
                    _headers[headerName] = headerValue;
            }
        }

        #endregion

        #region Manual parsing of Cookies with extended attributes

        private void ParseCookieFromHeader(string headerValue)
        {
            if (!_request.UseCookies)
                return;

            Cookies.Set(_request.Address, headerValue);
        }

        #endregion

        #region Loading the message body

        private IEnumerable<BytesWrapper> GetMessageBodySource()
        {
            var result = _headers.ContainsKey("Content-Encoding") && _headers["Content-Encoding"].Equals("gzip", StringComparison.OrdinalIgnoreCase)
                ? GetMessageBodySourceZip()
                : GetMessageBodySourceStd();

            return result; // .ToArray(); - it will break Chunked requests.
        }

        // Loading normal data.
        private IEnumerable<BytesWrapper> GetMessageBodySourceStd()
        {
            return _headers.ContainsKey("Transfer-Encoding")
                ? ReceiveMessageBodyChunked()
                : ContentLength != -1 ? ReceiveMessageBody(ContentLength) : ReceiveMessageBody(_request.ClientStream);
        }

        // Loading compressed data.
        private IEnumerable<BytesWrapper> GetMessageBodySourceZip()
        {
            if (_headers.ContainsKey("Transfer-Encoding"))
                return ReceiveMessageBodyChunkedZip();

            if (ContentLength != -1)
                return ReceiveMessageBodyZip(ContentLength);

            var streamWrapper = new ZipWrapperStream(
                _request.ClientStream, _receiverHelper);

            return ReceiveMessageBody(GetZipStream(streamWrapper));
        }

        private static byte[] GetResponse(Stream stream)
        {
            using (var memoryStream = new MemoryStream())
            {
                stream.CopyTo(memoryStream);
                return memoryStream.ToArray();
            }
        }

        // Loading a message body of unknown length.
        private static IEnumerable<BytesWrapper> ReceiveMessageBody(Stream stream)
        {
            // It's a fix of response get stuck response issue #83: https://github.com/csharp-leaf/Leaf.xNet/issues/83
            var bytesWrapper = new BytesWrapper();
            var responseBytes = GetResponse(stream);
            bytesWrapper.Value = responseBytes;
            bytesWrapper.Length = responseBytes.Length;
            return new[] { bytesWrapper };
        }

        /*
        private IEnumerable<BytesWrapper> ReceiveMessageBody(Stream stream)
        {
            var bytesWrapper = new BytesWrapper();

            int bufferSize = _request.TcpClient.ReceiveBufferSize;
            var buffer = new byte[bufferSize];

            bytesWrapper.Value = buffer;

            int begBytesRead = 0;

            // Считываем начальные данные из тела сообщения.
            if (stream is GZipStream || stream is DeflateStream)
                begBytesRead = stream.Read(buffer, 0, bufferSize);
            else
            {
                if (_receiverHelper.HasData)
                    begBytesRead = _receiverHelper.Read(buffer, 0, bufferSize);

                if (begBytesRead < bufferSize)
                    begBytesRead += stream.Read(buffer, begBytesRead, bufferSize - begBytesRead);
            }

            // Возвращаем начальные данные.
            bytesWrapper.Length = begBytesRead;
            yield return bytesWrapper;

            // Проверяем, есть ли открывающий тег '<html'.
            // Если есть, то считываем данные то тех пор, пока не встретим закрывающий тек '</html>'.
            bool isHtml = FindSignature(buffer, begBytesRead, OpenHtmlSignature);

            if (isHtml)
            {
                bool found = FindSignature(buffer, begBytesRead, CloseHtmlSignature);

                // Проверяем, есть ли в начальных данных закрывающий тег.
                if (found)
                    yield break;
            }

            while (true)
            {
                int bytesRead = stream.Read(buffer, 0, bufferSize);

                // Если тело сообщения представляет HTML.
                if (isHtml)
                {
                    if (bytesRead == 0)
                    {
                        WaitData();
                        continue;
                    }

                    bool found = FindSignature(buffer, bytesRead, CloseHtmlSignature);

                    if (found)
                    {
                        bytesWrapper.Length = bytesRead;
                        yield return bytesWrapper;

                        yield break;
                    }
                }
                else if (bytesRead == 0)
                    yield break;

                bytesWrapper.Length = bytesRead;
                yield return bytesWrapper;
            }
        }
        */

        // Loading a message body of known length.
        private IEnumerable<BytesWrapper> ReceiveMessageBody(long contentLength)
        {
            var stream = _request.ClientStream;
            var bytesWrapper = new BytesWrapper();

            int bufferSize = _request.TcpClient.ReceiveBufferSize;
            var buffer = new byte[bufferSize];

            bytesWrapper.Value = buffer;

            int totalBytesRead = 0;

            while (totalBytesRead != contentLength)
            {
                int bytesRead = _receiverHelper.HasData ? _receiverHelper.Read(buffer, 0, bufferSize) : stream.Read(buffer, 0, bufferSize);

                if (bytesRead == 0)
                    WaitData();
                else
                {
                    totalBytesRead += bytesRead;

                    bytesWrapper.Length = bytesRead;
                    yield return bytesWrapper;
                }
            }
        }

        // Loading the body of the message in parts.
        private IEnumerable<BytesWrapper> ReceiveMessageBodyChunked()
        {
            var stream = _request.ClientStream;
            var bytesWrapper = new BytesWrapper();

            int bufferSize = _request.TcpClient.ReceiveBufferSize;
            var buffer = new byte[bufferSize];

            bytesWrapper.Value = buffer;

            while (true)
            {
                string line = _receiverHelper.ReadLine();

                // If the end of the block is reached.
                if (line == Utility.NewLine)
                    continue;

                line = line.Trim(' ', '\r', '\n');

                // If the end of the message body is reached.
                if (line == string.Empty)
                    yield break;

                int blockLength;
                int totalBytesRead = 0;

                #region Set the block length

                try
                {
                    blockLength = Convert.ToInt32(line, 16);
                }
                catch (Exception ex)
                {
                    if (ex is FormatException || ex is OverflowException)
                    {
                        throw NewHttpException(string.Format(Constants.HttpException_WrongChunkedBlockLength, line), ex);
                    }

                    throw;
                }

                #endregion

                // If the end of the message body is reached.
                if (blockLength == 0)
                    yield break;

                while (totalBytesRead != blockLength)
                {
                    int length = blockLength - totalBytesRead;

                    if (length > bufferSize)
                        length = bufferSize;

                    int bytesRead = _receiverHelper.HasData
                        ? _receiverHelper.Read(buffer, 0, length)
                        : stream.Read(buffer, 0, length);

                    if (bytesRead == 0)
                        WaitData();
                    else
                    {
                        totalBytesRead += bytesRead;

                        bytesWrapper.Length = bytesRead;
                        yield return bytesWrapper;
                    }
                }
            }
        }

        private IEnumerable<BytesWrapper> ReceiveMessageBodyZip(long contentLength)
        {
            var bytesWrapper = new BytesWrapper();
            var streamWrapper = new ZipWrapperStream(
                _request.ClientStream, _receiverHelper);

            using (var stream = GetZipStream(streamWrapper))
            {
                int bufferSize = _request.TcpClient.ReceiveBufferSize;
                var buffer = new byte[bufferSize];

                bytesWrapper.Value = buffer;

                while (true)
                {
                    int bytesRead = stream.Read(buffer, 0, bufferSize);

                    if (bytesRead == 0)
                    {
                        if (streamWrapper.TotalBytesRead == contentLength)
                            yield break;

                        WaitData();

                        continue;
                    }

                    bytesWrapper.Length = bytesRead;
                    yield return bytesWrapper;
                }
            }
        }

        private IEnumerable<BytesWrapper> ReceiveMessageBodyChunkedZip()
        {
            var bytesWrapper = new BytesWrapper();
            var streamWrapper = new ZipWrapperStream
                (_request.ClientStream, _receiverHelper);

            using (var stream = GetZipStream(streamWrapper))
            {
                int bufferSize = _request.TcpClient.ReceiveBufferSize;
                var buffer = new byte[bufferSize];

                bytesWrapper.Value = buffer;

                while (true)
                {
                    string line = _receiverHelper.ReadLine();

                    // If the end of the block is reached.
                    if (line == Utility.NewLine)
                        continue;

                    line = line.Trim(' ', '\r', '\n');

                    // If the end of the message body is reached.
                    if (line == string.Empty)
                        yield break;

                    int blockLength;

                    #region Set the block length

                    try
                    {
                        blockLength = Convert.ToInt32(line, 16);
                    }
                    catch (Exception ex)
                    {
                        if (ex is FormatException || ex is OverflowException)
                        {
                            throw NewHttpException(string.Format(Constants.HttpException_WrongChunkedBlockLength, line), ex);
                        }

                        throw;
                    }

                    #endregion

                    // If the end of the message body is reached.
                    if (blockLength == 0)
                        yield break;

                    streamWrapper.TotalBytesRead = 0;
                    streamWrapper.LimitBytesRead = blockLength;

                    while (true)
                    {
                        int bytesRead = stream.Read(buffer, 0, bufferSize);

                        if (bytesRead == 0)
                        {
                            if (streamWrapper.TotalBytesRead == blockLength)
                                break;

                            WaitData();

                            continue;
                        }

                        bytesWrapper.Length = bytesRead;
                        yield return bytesWrapper;
                    }
                }
            }
        }

        #endregion

        #region Getting the value of HTTP headers

        private bool ConnectionClosed()
        {
            return _headers.ContainsKey("Connection") &&
                   _headers["Connection"].Equals("close", StringComparison.OrdinalIgnoreCase) ||
                   _headers.ContainsKey("Proxy-Connection") &&
                   _headers["Proxy-Connection"].Equals("close", StringComparison.OrdinalIgnoreCase);
        }

        private int? GetKeepAliveTimeout()
        {
            if (!_headers.ContainsKey("Keep-Alive"))
                return null;

            string header = _headers["Keep-Alive"];
            var match = KeepAliveTimeoutRegex.Match(header);

            if (match.Success)
                return int.Parse(match.Groups["value"].Value) * 1000; // In milliseconds.

            return null;
        }

        private int? GetKeepAliveMax()
        {
            if (!_headers.ContainsKey("Keep-Alive"))
                return null;

            string header = _headers["Keep-Alive"];
            var match = KeepAliveMaxRegex.Match(header);

            if (match.Success)
                return int.Parse(match.Groups["value"].Value);

            return null;
        }

        private Uri GetLocation()
        {
            if (!_headers.TryGetValue("Location", out string location))
                _headers.TryGetValue("Redirect-Location", out location);

            if (string.IsNullOrEmpty(location))
                return null;

            var baseAddress = _request.Address;
            Uri.TryCreate(baseAddress, location, out var redirectAddress);

            return redirectAddress;
        }

        private Encoding GetCharacterSet()
        {
            if (!_headers.ContainsKey("Content-Type"))
                return _request.CharacterSet ?? Encoding.Default;

            string header = _headers["Content-Type"];
            var match = ContentCharsetRegex.Match(header);

            if (!match.Success)
                return _request.CharacterSet ?? Encoding.Default;

            var charset = match.Groups["value"];

            try
            {
                return Encoding.GetEncoding(charset.Value);
            }
            catch (ArgumentException)
            {
                return _request.CharacterSet ?? Encoding.Default;
            }
        }

        private long GetContentLength()
        {
            string contentLengthHeader = Utility.Headers[HttpHeader.ContentLength];

            if (!_headers.ContainsKey(contentLengthHeader))
                return -1;

            if (!long.TryParse(_headers[contentLengthHeader], out long contentLength))
                throw new FormatException($"Invalid response header \"{contentLengthHeader}\" value");

            return contentLength;
        }

        private string GetContentType()
        {
            string contentTypeHeader = Utility.Headers[HttpHeader.ContentType];

            if (!_headers.ContainsKey(contentTypeHeader))
                return string.Empty;

            string contentType = _headers[contentTypeHeader];

            // We are looking for the position where the description of the content type ends and the description of its parameters begins.
            int endTypePos = contentType.IndexOf(';');
            if (endTypePos != -1)
                contentType = contentType.Substring(0, endTypePos);

            return contentType;
        }

        #endregion

        private void WaitData()
        {
            int sleepTime = 0;
            int delay = _request.TcpClient.ReceiveTimeout < 10
                ? 10
                : _request.TcpClient.ReceiveTimeout;

            while (!_request.ClientNetworkStream.DataAvailable)
            {
                if (sleepTime >= delay)
                    throw NewHttpException(Constants.HttpException_WaitDataTimeout);

                sleepTime += 10;
                Thread.Sleep(10);
            }
        }

        private Stream GetZipStream(Stream stream)
        {
            string contentEncoding = _headers[Utility.Headers[HttpHeader.ContentEncoding]].ToLower();

            switch (contentEncoding)
            {
                case "gzip":
                    return new GZipStream(stream, CompressionMode.Decompress, true);

                case "deflate":
                    return new DeflateStream(stream, CompressionMode.Decompress, true);

                default:
                    throw new InvalidOperationException(string.Format(Constants.InvalidOperationException_NotSupportedEncodingFormat, contentEncoding));
            }
        }

        private static bool FindSignature(byte[] source, int sourceLength, byte[] signature)
        {
            int length = sourceLength - signature.Length + 1;

            for (int sourceIndex = 0; sourceIndex < length; ++sourceIndex)
            {
                for (int signatureIndex = 0; signatureIndex < signature.Length; ++signatureIndex)
                {
                    byte sourceByte = source[signatureIndex + sourceIndex];
                    char sourceChar = (char)sourceByte;

                    if (char.IsLetter(sourceChar))
                        sourceChar = char.ToLower(sourceChar);

                    sourceByte = (byte)sourceChar;

                    if (sourceByte != signature[signatureIndex])
                        break;

                    if (signatureIndex == signature.Length - 1)
                        return true;
                }
            }

            return false;
        }

        private HttpException NewHttpException(string message, Exception innerException = null)
        {
            return new HttpException(string.Format(message, Address.Host), HttpExceptionStatus.ReceiveFailure, 0, innerException);
        }

        #endregion
    }
}
