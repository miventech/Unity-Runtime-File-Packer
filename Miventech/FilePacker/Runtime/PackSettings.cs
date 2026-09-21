using System.IO;

namespace Miventech.FilePacker
{
    /// <summary>
    /// Constantes del formato FilePacker v6.
    /// </summary>
    public static class PackSettings
    {
        public const string IndexExtension = ".ipk";
        public const string ChunkExtension = ".pkcam";
        public const string ChunkPrefix = "_data_";
        public const long MaxChunkSize = 4L * 1024 * 1024 * 1024; // 4 GB por chunk
        public const int DefaultKdfIterations = 100000;

        public static string GetChunkFileName(string indexPath, long chunkIndex)
        {
            return Path.GetFileNameWithoutExtension(indexPath) + ChunkPrefix + chunkIndex + ChunkExtension;
        }
    }
}
