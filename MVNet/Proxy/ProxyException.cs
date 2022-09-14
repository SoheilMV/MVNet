using System;
using System.Runtime.Serialization;

namespace MVNet
{
    /// <inheritdoc />
    /// <summary>
    /// The exception that is thrown when an error occurs while working with the proxy.
    /// </summary>
    [Serializable]
    public sealed class ProxyException : NetException
    {
        /// <summary>
        /// Gets the proxy client in which the error occurred.
        /// </summary>
        public ProxyClient ProxyClient { get; }


        #region Constructors (public)

        /// <inheritdoc />
        /// <summary>
        /// Initializes a new instance of the class <see cref="T:MVNet.ProxyException" />.
        /// </summary>
        public ProxyException() : this(Constants.ProxyException_Default)
        {
        }

        /// <inheritdoc />
        /// <summary>
        /// Initializes a new instance of the class <see cref="T:Leaf.xNet.ProxyException" /> given error message.
        /// </summary>
        /// <param name="message">Error message explaining the reason for the exception.</param>
        /// <param name="innerException">The exception that threw the current exception, or the value <see langword="null" />.</param>
        public ProxyException(string message, Exception innerException = null) : base(message, innerException)
        {
        }

        /// <inheritdoc />
        /// <summary>
        /// Initializes a new instance of the class <see cref="!:Leaf.xNet.Net.ProxyException" /> the specified error message and client proxy.
        /// </summary>
        /// <param name="message">Error message explaining the reason for the exception.</param>
        /// <param name="proxyClient">The proxy client in which the error occurred.</param>
        /// <param name="innerException">The exception that threw the current exception, or the value <see langword="null" />.</param>
        public ProxyException(string message, ProxyClient proxyClient, Exception innerException = null) : base(message, innerException)
        {
            ProxyClient = proxyClient;
        }

        #endregion


        /// <inheritdoc />
        /// <summary>
        /// Initializes a new instance of the class <see cref="T:MVNet.ProxyException" /> given instances <see cref="T:System.Runtime.Serialization.SerializationInfo" /> and <see cref="T:System.Runtime.Serialization.StreamingContext" />.
        /// </summary>
        /// <param name="serializationInfo">Class instance <see cref="T:System.Runtime.Serialization.SerializationInfo" />, which contains the information required to serialize a new instance of the class <see cref="T:MVNet.ProxyException" />.</param>
        /// <param name="streamingContext">Class instance <see cref="T:System.Runtime.Serialization.StreamingContext" />, containing the source of the serialized stream associated with the new instance of the class <see cref="T:MVNet.ProxyException" />.</param>
        public ProxyException(SerializationInfo serializationInfo, StreamingContext streamingContext) : base(serializationInfo, streamingContext)
        {
        }
    }
}
