using System.Runtime.Serialization;

namespace MVNet
{
    /// <inheritdoc />
    /// <summary>
    /// The exception that is thrown when an error occurs while working with the network.
    /// </summary>
    [Serializable]
    public class NetException : Exception
    {
        #region Constructors (public)

        /// <inheritdoc />
        /// <summary>
        /// Initializes a new instance of the class <see cref="T:MVNet.NetException" />.
        /// </summary>
        public NetException() : this(Constants.NetException_Default) { }

        /// <inheritdoc />
        /// <summary>
        /// Initializes a new instance of the class <see cref="T:MVNet.NetException" /> given error message.
        /// </summary>
        /// <param name="message">Error message explaining the reason for the exception.</param>
        /// <param name="innerException">The exception that threw the current exception, or the value <see langword="null" />.</param>
        public NetException(string message, Exception innerException = null)
            : base(message, innerException) { }

        #endregion


        /// <inheritdoc />
        /// <summary>
        /// Initializes a new instance of the class <see cref="T:MVNet.NetException" /> given instances <see cref="T:System.Runtime.Serialization.SerializationInfo" /> and <see cref="T:System.Runtime.Serialization.StreamingContext" />.
        /// </summary>
        /// <param name="serializationInfo">Class instance <see cref="T:System.Runtime.Serialization.SerializationInfo" />, which contains the information required to serialize a new instance of the class <see cref="T:MVNet.NetException" />.</param>
        /// <param name="streamingContext">Class instance <see cref="T:System.Runtime.Serialization.StreamingContext" />, containing the source of the serialized stream associated with the new instance of the class <see cref="T:MVNet.NetException" />.</param>
        protected NetException(SerializationInfo serializationInfo, StreamingContext streamingContext) : base(serializationInfo, streamingContext) { }
    }
}
