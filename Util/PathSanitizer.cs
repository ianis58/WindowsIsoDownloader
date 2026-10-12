namespace WindowsIsoDownloader.Util
{
    /// <summary>Helpers to keep user supplied file names inside the output folder.</summary>
    public static class PathSanitizer
    {
        private static readonly char[] ExplicitlyForbidden = { '/', '\\', '\0' };

        /// <summary>
        /// Ensures <paramref name="fileName"/> is a plain file name (no directory
        /// separators, no traversal, no invalid characters). Throws
        /// <see cref="ArgumentException"/> otherwise.
        /// </summary>
        public static void ValidateFileName(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                throw new ArgumentException("The file name must not be empty.", nameof(fileName));
            }

            if (fileName.IndexOfAny(ExplicitlyForbidden) >= 0
                || fileName.Contains("..", StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    $"The file name must be a plain name without path separators or '..': '{fileName}'",
                    nameof(fileName));
            }

            if (Path.IsPathRooted(fileName))
            {
                throw new ArgumentException($"The file name must not be rooted: '{fileName}'", nameof(fileName));
            }

            if (fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                throw new ArgumentException($"The file name contains invalid characters: '{fileName}'", nameof(fileName));
            }
        }

        /// <summary>Combines the folder and file name into a normalized absolute path.</summary>
        public static string ResolveOutputPath(string folder, string fileName)
        {
            ValidateFileName(fileName);

            if (string.IsNullOrWhiteSpace(folder))
            {
                throw new ArgumentException("The output folder must not be empty.", nameof(folder));
            }

            return Path.GetFullPath(Path.Combine(folder, fileName));
        }
    }
}
