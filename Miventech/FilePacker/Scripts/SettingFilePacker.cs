namespace Miventech.FilePacker
{
    public static class SettingFilePacker
    {
        public const string ExtensionChunkFile = ".pkcam"; // Miventech packed data
        public const string ExtensionIndexFile = ".ipk";   // Miventech index file
        public const long MAX_CHUNK_SIZE = 4L * 1024 * 1024 * 1024; // 4 GB per chunk limit
        
        // --- Security ---
        // HEADS UP: Change this key before you go live. Needs 16, 24, or 32 chars for AES to be happy.
        public const string EncryptionKey = "MiventechPackerDefaultKey1234567"; 
    }
}