namespace MVNet
{
    /// <summary>
    /// Defines states for a class <see cref="HttpException"/>.
    /// </summary>
    public enum HttpExceptionStatus
    {
        /// <summary>
        /// Another error has occurred.
        /// </summary>
        Other,
        /// <summary>
        /// The response received from the server was complete but indicated a protocol level error. Let's say the server returned a 404 or Not Found error.
        /// </summary>
        ProtocolError,
        /// <summary>
        /// Failed to connect to HTTP server.
        /// </summary>
        ConnectFailure,
        /// <summary>
        /// Failed to send request to HTTP server.
        /// </summary>
        SendFailure,
        /// <summary>
        /// Failed to load response from HTTP server.
        /// </summary>
        ReceiveFailure
    }
}
