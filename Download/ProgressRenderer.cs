using System.Globalization;
using WindowsIsoDownloader.Util;

namespace WindowsIsoDownloader.Download
{
    /// <summary>Renders the download progress bar (with speed and ETA).</summary>
    public sealed class ProgressRenderer
    {
        private readonly int _barWidth;
        private readonly bool _interactive;
        private double _lastInteractivePercent = -1;
        private int _lastNonInteractiveBucket = -1;

        public ProgressRenderer(int barWidth = 40)
        {
            _barWidth = Math.Max(1, barWidth);
            try
            {
                _interactive = !Console.IsOutputRedirected;
            }
            catch (IOException)
            {
                _interactive = false;
            }
        }

        /// <summary>Updates the progress bar.</summary>
        public void Report(DownloadProgress progress)
        {
            var ratio = progress.Percent ?? 0;

            if (_interactive)
            {
                var percent = ratio * 100;
                if (ratio < 1 && Math.Abs(percent - _lastInteractivePercent) < 0.1)
                {
                    return;
                }

                _lastInteractivePercent = percent;
                WriteBar(progress, ratio, newline: false);
            }
            else
            {
                var bucket = (int)(ratio * 10);
                if (bucket <= _lastNonInteractiveBucket && ratio < 1)
                {
                    return;
                }

                _lastNonInteractiveBucket = bucket;
                WriteBar(progress, ratio, newline: true);
            }
        }

        /// <summary>Finishes the line.</summary>
        public void Complete(DownloadProgress progress)
        {
            if (_interactive)
            {
                WriteBar(progress, progress.Percent ?? 1, newline: false);
            }

            Console.WriteLine();
        }

        private void WriteBar(DownloadProgress progress, double ratio, bool newline)
        {
            ratio = Math.Clamp(ratio, 0, 1);
            var filled = (int)Math.Round(ratio * _barWidth);
            var bar = new string('█', filled) + new string('░', _barWidth - filled);
            var percentText = progress.Percent.HasValue
                ? (ratio * 100).ToString("0.0", CultureInfo.InvariantCulture).PadLeft(5) + "%"
                : "  ?  ";
            var sizeText = progress.TotalBytes.HasValue
                ? $"{ByteFormatter.FormatBytes(progress.BytesReceived)} / {ByteFormatter.FormatBytes(progress.TotalBytes.Value)}"
                : ByteFormatter.FormatBytes(progress.BytesReceived);
            var line = $"[{bar}] {percentText}  {sizeText}  {ByteFormatter.FormatSpeed(progress.BytesPerSecond)}  ETA {ByteFormatter.FormatEta(progress.Eta)}";

            if (newline)
            {
                Console.WriteLine(line);
                return;
            }

            line = PadToConsoleWidth(line);
            Console.Write('\r');
            Console.Write(line);
        }

        private static string PadToConsoleWidth(string line)
        {
            try
            {
                if (Console.IsOutputRedirected)
                {
                    return line;
                }

                var width = Math.Max(0, Console.WindowWidth - 1);
                return line.Length < width ? line.PadRight(width) : line;
            }
            catch (IOException)
            {
                return line;
            }
        }
    }
}
