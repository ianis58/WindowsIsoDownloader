using System.Globalization;

namespace WindowsIsoDownloader.Util
{
    /// <summary>Human readable formatting for byte sizes, transfer speeds and ETAs.</summary>
    public static class ByteFormatter
    {
        private static readonly string[] Units = { "B", "KB", "MB", "GB", "TB" };

        /// <summary>Formats a byte count, for example <c>1.98 GB</c>.</summary>
        public static string FormatBytes(long bytes)
        {
            if (bytes < 0)
            {
                return "?";
            }

            double value = bytes;
            var unit = 0;
            while (value >= 1024 && unit < Units.Length - 1)
            {
                value /= 1024;
                unit++;
            }

            // Invariant culture keeps the decimal separator as '.', regardless of the OS locale.
            return unit == 0
                ? $"{bytes.ToString(CultureInfo.InvariantCulture)} B"
                : $"{value.ToString("0.00", CultureInfo.InvariantCulture)} {Units[unit]}";
        }

        /// <summary>Formats a transfer speed, for example <c>12.3 MB/s</c>.</summary>
        public static string FormatSpeed(double bytesPerSecond)
        {
            if (!double.IsFinite(bytesPerSecond) || bytesPerSecond <= 0)
            {
                return "--";
            }

            return $"{FormatBytes((long)bytesPerSecond)}/s";
        }

        /// <summary>Formats an ETA as <c>hh:mm:ss</c>, or <c>--:--:--</c> when unknown.</summary>
        public static string FormatEta(TimeSpan? eta)
        {
            if (eta is { TotalSeconds: > 0 and < 359999 })
            {
                return eta.Value.ToString(@"hh\:mm\:ss", CultureInfo.InvariantCulture);
            }

            return eta is { TotalSeconds: <= 0 } ? "00:00:00" : "--:--:--";
        }
    }
}
