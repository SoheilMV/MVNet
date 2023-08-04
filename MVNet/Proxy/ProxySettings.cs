using System;
using System.Net;

namespace MVNet
{
    /// <summary>
    /// Settings for <see cref="ProxyClient"/>.
    /// </summary>
    public class ProxySettings
    {
        /// <summary>
        /// Gets or sets the credentials to submit to the proxy server for authentication.
        /// </summary>
        public NetworkCredential Credentials { get; set; }

        /// <summary>
        /// The hostname or ip of the proxy server.
        /// </summary>
        public string Host { get; set; }

        /// <summary>
        /// The port on which the proxy server is listening.
        /// </summary>
        public int Port { get; set; }
    }
}
