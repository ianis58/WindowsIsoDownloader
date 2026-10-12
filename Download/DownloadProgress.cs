namespace WindowsIsoDownloader.Download
{
    /// <summary>Snapshot of an in-flight download.</summary>
    /// <param name="BytesReceived">Total bytes received so far (including resumed bytes).</param>
    /// <param name="TotalBytes">Expected total size, or <c>null</c> when unknown.</param>
    /// <param name="BytesPerSecond">Average transfer speed for the current session.</param>
    public readonly record struct DownloadProgress(long BytesReceived, long? TotalBytes, double BytesPerSecond)
    {
        /// <summary>Fraction downloaded (0.0 - 1.0), or <c>null</c> when the total is unknown.</summary>
        public double? Percent => TotalBytes is > 0 ? (double)BytesReceived / TotalBytes.Value : null;

        /// <summary>Estimated time remaining, or <c>null</c> when it cannot be computed.</summary>
        public TimeSpan? Eta
        {
            get
            {
                if (TotalBytes is not > 0 || BytesPerSecond <= 0)
                {
                    return null;
                }

                var remaining = TotalBytes.Value - BytesReceived;
                return remaining <= 0 ? TimeSpan.Zero : TimeSpan.FromSeconds(remaining / BytesPerSecond);
            }
        }
    }
}
