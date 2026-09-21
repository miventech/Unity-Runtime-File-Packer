namespace Miventech.FilePacker.Tools
{
    /// <summary>
    /// CRC32 (IEEE, polinomio 0xEDB88320) sin dependencias externas.
    /// Se calcula sobre el contenido PLANO (antes de comprimir/cifrar)
    /// y se verifica al leer para detectar corrupción del paquete.
    /// </summary>
    public static class Crc32
    {
        private static readonly uint[] Table = BuildTable();

        private static uint[] BuildTable()
        {
            var table = new uint[256];
            for (uint i = 0; i < 256; i++)
            {
                uint c = i;
                for (int j = 0; j < 8; j++)
                {
                    c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
                }
                table[i] = c;
            }
            return table;
        }

        public static uint Compute(byte[] data, int offset = 0, int count = -1)
        {
            if (data == null) return 0;
            if (count < 0) count = data.Length - offset;
            uint crc = 0xFFFFFFFFu;
            for (int i = offset; i < offset + count; i++)
            {
                crc = Table[(crc ^ data[i]) & 0xFF] ^ (crc >> 8);
            }
            return crc ^ 0xFFFFFFFFu;
        }
    }
}
