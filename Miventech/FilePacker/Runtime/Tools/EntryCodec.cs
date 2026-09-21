using System;
using System.IO;
using System.IO.Compression;
using K4os.Compression.LZ4;

namespace Miventech.FilePacker
{
    /// <summary>
    /// Compresión por entrada. LZ4 vía K4os.Compression.LZ4 (C# puro, multiplataforma:
    /// Windows/macOS/Linux/Android/iOS/WebGL, Mono e IL2CPP) con formato "pickle"
    /// auto-descriptivo; Deflate siempre disponible como alternativa.
    /// </summary>
    public static class EntryCodec
    {
        public static byte[] Compress(byte[] data, EntryCodecMode mode)
        {
            if (data == null || data.Length == 0 || mode == EntryCodecMode.None) return data;
            switch (mode)
            {
                case EntryCodecMode.Deflate: return CompressDeflate(data);
                case EntryCodecMode.Lz4: return LZ4Pickler.Pickle(data);
                default: throw new ArgumentOutOfRangeException(nameof(mode), mode, "Codec desconocido.");
            }
        }

        public static byte[] Decompress(byte[] data, EntryCodecMode mode, int uncompressedLength)
        {
            if (data == null || data.Length == 0 || mode == EntryCodecMode.None) return data;
            switch (mode)
            {
                case EntryCodecMode.Deflate: return DecompressDeflate(data, uncompressedLength);
                case EntryCodecMode.Lz4: return LZ4Pickler.Unpickle(data);
                default: throw new ArgumentOutOfRangeException(nameof(mode), mode, "Codec desconocido.");
            }
        }

        private static byte[] CompressDeflate(byte[] data)
        {
            using (var output = new MemoryStream())
            {
                using (var deflate = new DeflateStream(output, CompressionMode.Compress))
                {
                    deflate.Write(data, 0, data.Length);
                }
                // MemoryStream.ToArray() es válido aunque el stream esté cerrado
                return output.ToArray();
            }
        }

        private static byte[] DecompressDeflate(byte[] data, int uncompressedLength)
        {
            using (var input = new MemoryStream(data))
            using (var deflate = new DeflateStream(input, CompressionMode.Decompress))
            {
                byte[] buffer = uncompressedLength > 0 ? new byte[uncompressedLength] : new byte[4096];
                int total = 0;
                while (true)
                {
                    if (total == buffer.Length) Array.Resize(ref buffer, buffer.Length * 2);
                    int read = deflate.Read(buffer, total, buffer.Length - total);
                    if (read <= 0) break;
                    total += read;
                }
                if (total == buffer.Length) return buffer;
                byte[] result = new byte[total];
                Buffer.BlockCopy(buffer, 0, result, 0, total);
                return result;
            }
        }
    }
}
