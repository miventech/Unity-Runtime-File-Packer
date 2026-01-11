namespace Miventech.FilePacker.Tools
{
    public static class HashUtils
    {
        // FNV-1a 64-bit algorithm (Fast, stable, and reliable).
        // Don't use string.GetHashCode()! That one can change between .NET versions and ruin your index.
        public static long GetStableHash(string text)
        {
            if (string.IsNullOrEmpty(text)) return 0;

            ulong hash = 14695981039346656037; // Offset basis
            
            // Force lowercase and normalize slashes so we don't have issues with case or path separators
            text = text.ToLowerInvariant().Replace("\\", "/"); 

            for (int i = 0; i < text.Length; i++)
            {
                hash ^= text[i];
                hash *= 1099511628211; // Prime
            }
            // Return as long because it's generally friendlier to work with in C# than ulong
            return (long)hash;
        }
    }
}