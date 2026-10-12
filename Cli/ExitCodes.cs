namespace WindowsIsoDownloader.Cli
{
    /// <summary>
    /// Process exit codes. They make the outcome scriptable: each failure class
    /// has its own code so callers can react accordingly.
    /// </summary>
    public static class ExitCodes
    {
        /// <summary>Everything went well (or a dry run / listing finished).</summary>
        public const int Success = 0;

        /// <summary>Unexpected, unclassified error.</summary>
        public const int GeneralError = 1;

        /// <summary>The configuration is missing or invalid, or the page layout changed.</summary>
        public const int ConfigError = 2;

        /// <summary>Network failure while resolving or downloading.</summary>
        public const int NetworkError = 3;

        /// <summary>Microsoft's anti-bot protection (Sentinel) rejected the session.</summary>
        public const int AntiBotError = 4;

        /// <summary>The destination could not be written (permissions, I/O, diskspace).</summary>
        public const int DiskError = 5;

        /// <summary>The browser could not be started or its dependencies are missing.</summary>
        public const int BrowserError = 6;

        /// <summary>The user cancelled the operation (Ctrl+C).</summary>
        public const int Cancelled = 130;
    }
}
