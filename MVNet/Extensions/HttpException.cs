using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Runtime.Serialization;
using System.Security.Permissions;
using System.Text;
using System.Threading.Tasks;

namespace MVNet
{
    /// <inheritdoc />
    /// <summary>
    /// The exception that is thrown when an error occurs while working with the HTTP protocol.
    /// </summary>
    [Serializable]
    public sealed class HttpException : NetException
    {
        #region Properties (public)

        /// <summary>
        /// Returns the state of the exception.
        /// </summary>
        public HttpExceptionStatus Status { get; internal set; }

        /// <summary>
        /// Returns the response status code from the HTTP server.
        /// </summary>
        public HttpStatusCode HttpStatusCode { get; }

        #endregion


        internal bool EmptyMessageBody { get; set; }


        #region Constructors (public)

        /// <inheritdoc />
        /// <summary>
        /// Initializes a new instance of the class <see cref="T:MVNet.HttpException" />.
        /// </summary>
        public HttpException() : this(Constants.HttpException_Default)
        {
        }

        /// <inheritdoc />
        /// <summary>
        /// Initializes a new instance of the class <see cref="T:MVNet.HttpException" /> given error message.
        /// </summary>
        /// <param name="message">Error message explaining the reason for the exception.</param>
        /// <param name="innerException">The exception that threw the current exception, or the value <see langword="null" />.</param>
        public HttpException(string message, Exception innerException = null) : base(message, innerException)
        {
        }

        /// <inheritdoc />
        /// <summary>
        /// Initializes a new instance of the class <see cref="T:MVNet.HttpException" /> the specified error message and response status code.
        /// </summary>
        /// <param name="message">Error message explaining the reason for the exception.</param>
        /// <param name="status">HTTP Status of Thrown Exception</param>
        /// <param name="httpStatusCode">Status code of the response from the HTTP server.</param>
        /// <param name="innerException">The exception that threw the current exception, or the value <see langword="null" />.</param>
        public HttpException(string message, HttpExceptionStatus status, HttpStatusCode httpStatusCode = 0, Exception innerException = null) : base(message, innerException)
        {
            Status = status;
            HttpStatusCode = httpStatusCode;
        }

        #endregion


        /// <inheritdoc />
        /// <summary>
        /// Initializes a new instance of the class <see cref="T:MVNet.HttpException" /> given instances <see cref="T:System.Runtime.Serialization.SerializationInfo" /> и <see cref="T:System.Runtime.Serialization.StreamingContext" />.
        /// </summary>
        /// <param name="serializationInfo">Class instance <see cref="T:System.Runtime.Serialization.SerializationInfo" />, which contains the information required to serialize a new instance of the class <see cref="T:MVNet.HttpException" />.</param>
        /// <param name="streamingContext">Class instance <see cref="T:System.Runtime.Serialization.StreamingContext" />, containing the source of the serialized stream associated with the new instance of the class <see cref="T:MVNet.HttpException" />.</param>
        public HttpException(SerializationInfo serializationInfo, StreamingContext streamingContext) : base(serializationInfo, streamingContext)
        {
            if (serializationInfo == null)
                return;

            Status = (HttpExceptionStatus)serializationInfo.GetInt32("Status");
            HttpStatusCode = (HttpStatusCode)serializationInfo.GetInt32("HttpStatusCode");
        }


        /// <inheritdoc />
        /// <summary>
        /// Populates an instance <see cref="T:System.Runtime.Serialization.SerializationInfo" /> the data needed to serialize the exception <see cref="T:MVNet.HttpException" />.
        /// </summary>
        /// <param name="serializationInfo">Serialization data, <see cref="T:System.Runtime.Serialization.SerializationInfo" />, which should be used.</param>
        /// <param name="streamingContext">Serialization data, <see cref="T:System.Runtime.Serialization.StreamingContext" />, which should be used.</param>
        [SecurityPermission(SecurityAction.LinkDemand, Flags = SecurityPermissionFlag.SerializationFormatter)]
        public override void GetObjectData(SerializationInfo serializationInfo, StreamingContext streamingContext)
        {
            base.GetObjectData(serializationInfo, streamingContext);

            serializationInfo.AddValue("Status", (int)Status);
            serializationInfo.AddValue("HttpStatusCode", (int)HttpStatusCode);
            serializationInfo.AddValue("EmptyMessageBody", EmptyMessageBody);
        }
    }
}
