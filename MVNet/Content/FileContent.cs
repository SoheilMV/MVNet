namespace MVNet
{
    /// <inheritdoc />
    /// <summary>
    /// Represents the request body as a stream of data from a specific file.
    /// </summary>
    public class FileContent : StreamContent
    {
        /// <inheritdoc />
        /// <summary>
        /// Initializes a new instance of the class <see cref="T:MVNet.FileContent" /> and opens the file stream.
        /// </summary>
        /// <param name="pathToContent">The path to the file that will become the content of the request body.</param>
        /// <param name="bufferSize">The buffer size in bytes for the stream.</param>
        /// <exception cref="T:System.ArgumentNullException">Parameter value <paramref name="pathToContent" /> equals <see langword="null" />.</exception>
        /// <exception cref="T:System.ArgumentException">Parameter value <paramref name="pathToContent" /> is an empty string.</exception>
        /// <exception cref="T:System.ArgumentOutOfRangeException"> Parameter value <paramref name="bufferSize" /> less than 1.</exception>
        /// <exception cref="T:System.IO.PathTooLongException">The specified path, file name, or both exceeds the maximum length allowed by the system. For example, for Windows-based platforms, paths cannot exceed 248 characters and file names cannot exceed 260 characters.</exception>
        /// <exception cref="T:System.IO.FileNotFoundException">Parameter value <paramref name="pathToContent" /> points to a non-existent file.</exception>
        /// <exception cref="T:System.IO.DirectoryNotFoundException">Parameter value <paramref name="pathToContent" /> points to an invalid path.</exception>
        /// <exception cref="T:System.IO.IOException">I/O error while working with file.</exception>
        /// <exception cref="T:System.Security.SecurityException">The caller does not have the required permission.</exception>
        /// <exception cref="T:System.UnauthorizedAccessException">
        /// The file read operation is not supported on the current platform.
        /// -or-
        /// Parameter value <paramref name="pathToContent" /> defines a directory.
        /// -or-
        /// The caller does not have the required permission.
        /// </exception>
        /// <remarks>The content type is determined automatically based on the file extension.</remarks>
        public FileContent(string pathToContent, int bufferSize = 32768)
        {
            #region Parameter Check

            if (pathToContent == null)
                throw new ArgumentNullException(nameof(pathToContent));

            if (pathToContent.Length == 0)
                throw ExceptionHelper.EmptyString(nameof(pathToContent));

            if (bufferSize < 1)
                throw ExceptionHelper.CanNotBeLess(nameof(bufferSize), 1);

            #endregion

            ContentStream = new FileStream(pathToContent, FileMode.Open, FileAccess.Read);
            BufferSize = bufferSize;
            InitialStreamPosition = 0;

            MimeContentType = Utility.DetermineMediaType(Path.GetExtension(pathToContent));
        }
    }
}
