namespace MVNet
{
    /// <inheritdoc />
    /// <summary>
    /// Represents data for an event that reports the progress of loading data.
    /// </summary>
    public sealed class DownloadProgressChangedEventArgs : EventArgs
    {
        #region Properties (public)

        /// <summary>
        /// Returns the number of bytes received.
        /// </summary>
        public long BytesReceived { get; }

        /// <summary>
        /// Returns the total number of bytes received.
        /// </summary>
        /// <value>If the total number of bytes received is not known, then the value is -1.</value>
        public long TotalBytesToReceive { get; }

        /// <summary>
        /// Returns the percentage of bytes received.
        /// </summary>
        public double ProgressPercentage => (double)BytesReceived / TotalBytesToReceive * 100.0;

        #endregion


        /// <inheritdoc />
        /// <summary>
        /// Initializes a new instance of the class <see cref="T:MVNet.DownloadProgressChangedEventArgs" />.
        /// </summary>
        /// <param name="bytesReceived">The number of bytes received.</param>
        /// <param name="totalBytesToReceive">The total number of bytes received.</param>
        public DownloadProgressChangedEventArgs(long bytesReceived, long totalBytesToReceive)
        {
            BytesReceived = bytesReceived;
            TotalBytesToReceive = totalBytesToReceive;
        }
    }
}
