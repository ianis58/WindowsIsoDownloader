namespace WindowsIsoDownloader.Configuration
{
    /// <summary>Raised when <c>config.json</c> cannot be read or is invalid.</summary>
    public sealed class ConfigException : Exception
    {
        public ConfigException(string message)
            : base(message)
        {
        }

        public ConfigException(string message, Exception? innerException)
            : base(message, innerException)
        {
        }
    }
}
