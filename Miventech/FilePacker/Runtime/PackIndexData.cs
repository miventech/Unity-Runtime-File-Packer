using System.Collections.Generic;

namespace Miventech.FilePacker
{
    /// <summary>
    /// Contenido completo del índice de un paquete (entradas + sobre de clave maestra).
    /// </summary>
    public sealed class PackIndexData
    {
        public List<PackEntry> Entries = new List<PackEntry>();

        /// <summary>
        /// Sobre EasyCrypto que protege la clave maestra del paquete.
        /// Null si el paquete no usa cifrado.
        /// </summary>
        public byte[] MasterKeyBlob;

        public bool IsEncrypted
        {
            get
            {
                return MasterKeyBlob != null && MasterKeyBlob.Length > 0;
            }
        }
    }
}
