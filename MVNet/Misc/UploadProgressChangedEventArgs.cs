namespace MVNet
{
    /// <inheritdoc />
    /// <summary>
    /// Represents data for an event reporting the progress of the data upload.
    /// </summary>
    public sealed class UploadProgressChangedEventArgs : EventArgs
    {
        #region Properties (public)

        /// <summary>
        /// Returns the number of bytes sent.
        /// </summary>
        public long BytesSent { get; }

        /// <summary>
        /// Returns the total number of bytes sent.
        /// </summary>
        public long TotalBytesToSend { get; }

        /// <summary>
        /// Returns the percentage of bytes sent.
        /// </summary>
        public double ProgressPercentage => (double)BytesSent / TotalBytesToSend * 100.0;

        #endregion


        /// <inheritdoc />
        /// <summary>
        /// Initializes a new instance of the class <see cref="T:MVNet.UploadProgressChangedEventArgs" />.
        /// </summary>
        /// <param name="bytesSent">The number of bytes sent.</param>
        /// <param name="totalBytesToSend">The total number of bytes sent.</param>
        public UploadProgressChangedEventArgs(long bytesSent, long totalBytesToSend)
        {
            BytesSent = bytesSent;
            TotalBytesToSend = totalBytesToSend;
        }
    }
}
