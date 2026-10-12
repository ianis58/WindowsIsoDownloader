using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;

namespace WindowsIsoDownloader.Download
{
    /// <summary>ISO download with resume support and progress reporting.</summary>
    public static class HttpClientExtensions
    {
        /// <summary>Size of the copy buffer used while streaming the response body.</summary>
        public const int BufferSize = 81920;

        /// <summary>
        /// Downloads <paramref name="requestUri"/> into <paramref name="destinationPath"/>.
        /// <para>
        /// The transfer is written to <c>destinationPath + ".part"</c> first and moved
        /// into place only after it completes, so an interrupted run can be resumed:
        /// the next call sends a <c>Range</c> header and continues where it stopped.
        /// </para>
        /// </summary>
        /// <returns>The number of bytes written.</returns>
        public static async Task<long> DownloadFileAsync(
            this HttpClient client,
            string requestUri,
            string destinationPath,
            IProgress<DownloadProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(client);

            var partPath = destinationPath + ".part";
            long existingBytes = File.Exists(partPath) ? new FileInfo(partPath).Length : 0;

            using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
            if (existingBytes > 0)
            {
                request.Headers.Range = new RangeHeaderValue(existingBytes, null);
            }

            using var response = await client
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);

            if (response.StatusCode == HttpStatusCode.RequestedRangeNotSatisfiable)
            {
                // The .part file already holds at least as many bytes as the server
                // offers. If it matches the reported total, finalize it.
                var partLength = File.Exists(partPath) ? new FileInfo(partPath).Length : 0;
                var remoteTotal = response.Content.Headers.ContentRange?.Length;
                if (partLength > 0 && (remoteTotal is null || remoteTotal == partLength))
                {
                    Console.WriteLine("[Info] The existing .part file looks complete; finalizing it.");
                    return Finalize(partPath, destinationPath, partLength, progress);
                }

                throw new IOException(
                    $"The server rejected the resume request (HTTP 416) for '{partPath}'. " +
                    "Delete the .part file and run the tool again.");
            }

            if (existingBytes > 0 && response.StatusCode != HttpStatusCode.PartialContent)
            {
                // The server ignored the Range header; restart from the beginning.
                existingBytes = 0;
            }

            response.EnsureSuccessStatusCode();

            var mediaType = response.Content.Headers.ContentType?.MediaType ?? string.Empty;
            if (mediaType.StartsWith("text/", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    $"The server returned '{mediaType}' instead of an ISO image. " +
                    "The download link may have expired or the request was blocked.");
            }

            var remoteLength = response.Content.Headers.ContentLength;
            long? totalBytes = remoteLength.HasValue ? remoteLength.Value + existingBytes : null;

            var stopwatch = Stopwatch.StartNew();

            await using (var download = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false))
            await using (var file = new FileStream(
                partPath,
                existingBytes > 0 ? FileMode.Append : FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                BufferSize,
                useAsync: true))
            {
                var reporter = new Progress<long>(copied =>
                {
                    var seconds = stopwatch.Elapsed.TotalSeconds;
                    var speed = seconds > 0 ? copied / seconds : 0;
                    progress?.Report(new DownloadProgress(existingBytes + copied, totalBytes, speed));
                });

                await download.CopyToAsync(file, BufferSize, reporter, cancellationToken).ConfigureAwait(false);
                await file.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            var finalLength = new FileInfo(partPath).Length;
            if (totalBytes.HasValue && finalLength < totalBytes.Value)
            {
                throw new IOException(
                    $"The download is incomplete: {finalLength} of {totalBytes.Value} bytes were received. " +
                    "Run the tool again to resume.");
            }

            return Finalize(partPath, destinationPath, finalLength, progress);
        }

        /// <summary>Verifies, reports completion and atomically promotes the <c>.part</c> file.</summary>
        private static long Finalize(
            string partPath,
            string destinationPath,
            long length,
            IProgress<DownloadProgress>? progress)
        {
            progress?.Report(new DownloadProgress(length, length, 0));

            if (!IsoValidator.LooksLikeIso(partPath))
            {
                Console.WriteLine("[Warn] The downloaded file does not look like an ISO image (missing UDF/ISO9660 signature).");
            }

            File.Move(partPath, destinationPath, overwrite: true);
            return length;
        }
    }
}
