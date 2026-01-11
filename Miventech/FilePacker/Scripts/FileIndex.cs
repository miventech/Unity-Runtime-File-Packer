using System;

namespace Miventech.FilePacker
{
    [System.Serializable]
    public struct FileIndex
    {
        public int Id;
        public long NameHash; // FNV-1a hash of the filename
        public long Offset;
        public long Length;             // Size on disk (could be compressed/encrypted)
        public long UncompressedLength; // Original size before the magic happens
        public bool IsEncrypted;
        public bool IsCompressed;
    }
}