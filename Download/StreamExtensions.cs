namespace WindowsIsoDownloader.Download
{
    /// <summary>Stream helpers used by the downloader.</summary>
    public static class StreamExtensions
    {
        /// <summary>
        /// Copies <paramref name="source"/> into <paramref name="destination"/>,
        /// reporting the number of bytes copied so far.
        /// </summary>
        public static async Task<long> CopyToAsync(
            this Stream source,
            Stream destination,
            int bufferSize,
            IProgress<long>? progress,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(source);
            ArgumentNullException.ThrowIfNull(destination);

            if (!source.CanRead)
            {
                throw new ArgumentException("The source stream must be readable.", nameof(source));
            }

            if (!destination.CanWrite)
            {
                throw new ArgumentException("The destination stream must be writable.", nameof(destination));
            }

            if (bufferSize <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(bufferSize));
            }

            var buffer = new byte[bufferSize];
            long totalBytesRead = 0;
            int bytesRead;

            while ((bytesRead = await source.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken).ConfigureAwait(false)) != 0)
            {
                await destination.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken).ConfigureAwait(false);
                totalBytesRead += bytesRead;
                progress?.Report(totalBytesRead);
            }

            return totalBytesRead;
        }
    }
}
