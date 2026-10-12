using System.Text;

namespace WindowsIsoDownloader.Download
{
    /// <summary>Very small sanity check that a downloaded file is an optical disc image.</summary>
    public static class IsoValidator
    {
        /// <summary>
        /// Checks for the ISO9660 (<c>CD001</c>) or UDF (<c>BEA01</c>/<c>NSR02</c>/<c>NSR03</c>)
        /// volume recognition signature at sector 16. Windows ISOs are usually UDF,
        /// so both families are accepted.
        /// </summary>
        public static bool LooksLikeIso(Stream stream)
        {
            ArgumentNullException.ThrowIfNull(stream);

            if (!stream.CanSeek)
            {
                // Without random access we cannot inspect the signature; assume the best.
                return true;
            }

            const long signatureOffset = 0x8001; // sector 16, first byte of the identifier.
            if (stream.Length < signatureOffset + 5)
            {
                return false;
            }

            var position = stream.Position;
            try
            {
                stream.Position = signatureOffset;
                Span<byte> identifier = stackalloc byte[5];
                var read = stream.Read(identifier);
                if (read < 5)
                {
                    return false;
                }

                var text = Encoding.ASCII.GetString(identifier);
                return text is "CD001" or "BEA01" or "NSR02" or "NSR03";
            }
            finally
            {
                stream.Position = position;
            }
        }

        /// <summary>Convenience overload that opens the file for reading.</summary>
        public static bool LooksLikeIso(string path)
        {
            using var stream = File.OpenRead(path);
            return LooksLikeIso(stream);
        }
    }
}
