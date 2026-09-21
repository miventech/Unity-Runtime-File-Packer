using System;

namespace Miventech.FilePacker
{
    /// <summary>
    /// Opciones por defecto al crear/abrir un paquete con FilePack.
    /// Cada AddFile/AddData puede sobrescribir codec y cifrado por entrada.
    /// </summary>
    public sealed class PackOptions
    {
        public EntryCodecMode Codec = EntryCodecMode.Lz4;
        public bool Encrypt = false;
        public string Password = "";
        public int KdfIterations = PackSettings.DefaultKdfIterations;

        public PackOptions()
        {
        }

        public PackOptions(EntryCodecMode codec, bool encrypt, string password = "", int kdfIterations = PackSettings.DefaultKdfIterations)
        {
            Codec = codec;
            Encrypt = encrypt;
            Password = password ?? "";
            KdfIterations = Math.Max(1000, kdfIterations);
        }
    }
}
