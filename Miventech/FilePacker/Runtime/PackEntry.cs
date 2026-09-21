using System;

namespace Miventech.FilePacker
{
    /// <summary>
    /// Codec de compresión aplicado a cada entrada del paquete.
    /// LZ4 requiere el paquete com.unity.collections (define MIVENTECH_LZ4).
    /// </summary>
    public enum EntryCodecMode : byte
    {
        None = 0,
        Deflate = 1,
        Lz4 = 2,
    }

    /// <summary>
    /// Entrada del índice de un paquete FilePacker v6.
    /// </summary>
    [Serializable]
    public struct PackEntry
    {
        private const byte EncryptedFlag = 1;

        public int Id;
        public long NameHash;           // FNV-1a 64 del nombre normalizado
        public string Name;             // nombre normalizado (listado del contenido)
        public long Offset;             // offset global (virtual) dentro del paquete
        public long Length;             // bytes en disco (tras compresión + cifrado)
        public long UncompressedLength; // longitud original del contenido plano
        public byte Flags;              // bit 0: cifrado
        public byte Codec;              // EntryCodecMode
        public uint Checksum;           // CRC32 del contenido plano

        public bool IsEncrypted
        {
            get
            {
                return (Flags & EncryptedFlag) != 0;
            }
        }

        public EntryCodecMode CodecMode
        {
            get
            {
                return (EntryCodecMode)Codec;
            }
        }
    }
}
