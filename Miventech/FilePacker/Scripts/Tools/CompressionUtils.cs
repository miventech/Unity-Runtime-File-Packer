using System.IO;
using System.IO.Compression;

namespace Miventech.FilePacker.Tools
{
    // Quick helper for squashing data.
    // Right now it's using Deflate because it's built into .NET and "just works".
    // If you need more speed, you can swap this for LZ4 later.
    public static class CompressionUtils
    {
        // Squash those bytes!
        public static byte[] Compress(byte[] data)
        {
            if (data == null || data.Length == 0) return data;

            using (MemoryStream output = new MemoryStream())
            {
                using (DeflateStream dstream = new DeflateStream(output, CompressionMode.Compress))
                {
                    dstream.Write(data, 0, data.Length);
                }
                return output.ToArray();
            }
        }

        // Inflate them back to normal.
        public static byte[] Decompress(byte[] data)
        {
            if (data == null || data.Length == 0) return data;

            using (MemoryStream input = new MemoryStream(data))
            {
                using (MemoryStream output = new MemoryStream())
                {
                    using (DeflateStream dstream = new DeflateStream(input, CompressionMode.Decompress))
                    {
                        dstream.CopyTo(output);
                    }
                    return output.ToArray();
                }
            }
        }
    }
}
