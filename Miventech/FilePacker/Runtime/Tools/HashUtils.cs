namespace Miventech.FilePacker.Tools
{
    public static class HashUtils
    {
        /// <summary>
        /// Normaliza un nombre: minúsculas y '\' → '/' (misma regla del hash).
        /// El nombre normalizado se guarda en el índice para poder listar el contenido.
        /// </summary>
        public static string NormalizeName(string name)
        {
            if (string.IsNullOrEmpty(name)) return string.Empty;
            return name.ToLowerInvariant().Replace("\\", "/");
        }

        // FNV-1a 64-bit (rápido y estable).
        // NO usar string.GetHashCode(): cambia entre versiones de .NET y rompería el índice.
        public static long GetStableHash(string text)
        {
            string normalized = NormalizeName(text);
            if (normalized.Length == 0) return 0;

            ulong hash = 14695981039346656037; // Offset basis

            for (int i = 0; i < normalized.Length; i++)
            {
                hash ^= normalized[i];
                hash *= 1099511628211; // Prime
            }
            // Devolvemos como long porque es más cómodo en C# que ulong
            return (long)hash;
        }
    }
}
