using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace Miventech.FilePacker
{
    public static class FileIndexIO
    {
        // If we tweak the FileIndex struct, bump this version so we don't crash on old index files
        // Version 5: Added IsCompressed flag
        private const int INDEX_VERSION = 5;

        // Save the dictionary as binary. No JSON here, way too slow for what we need.
        // We're using the long hash as the unique key.
        public static void SaveIndex(string path, Dictionary<long, FileIndex> indexMap)
        {
            // Just in case, let's make sure the directory actually exists
            string directory = Path.GetDirectoryName(path);
            if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);

            using (FileStream fs = new FileStream(path, FileMode.Create, FileAccess.Write))
            using (BinaryWriter writer = new BinaryWriter(fs, Encoding.UTF8))
            {
                // Write version first so we can validate it later
                writer.Write(INDEX_VERSION);

                // Store total count so we know how many entries to pull back
                writer.Write(indexMap.Count);

                // Dump the data
                foreach (var entry in indexMap.Values)
                {
                    writer.Write(entry.Id);
                    writer.Write(entry.NameHash); // Write the name hash
                    writer.Write(entry.Offset);
                    writer.Write(entry.Length);
                    writer.Write(entry.UncompressedLength);
                    writer.Write(entry.IsEncrypted); 
                    writer.Write(entry.IsCompressed); // New in V5
                }
            }
            
            Debug.Log($"[FileIndexIO] Hashed index dumped at: {path} ({indexMap.Count} items)");
        }

        // Pull the dictionary back from its binary hibernation
        public static Dictionary<long, FileIndex> LoadIndex(string path, bool DynamicSizeDictionary = true)
        {
            if (!File.Exists(path))
            {
                Debug.LogError($"[FileIndexIO] Couldn't find the index file at: {path}");
                // Initialize an empty file to keep things quiet
                File.Create(path).Dispose();
                return new Dictionary<long, FileIndex>();
            }

            Dictionary<long, FileIndex> result;

            using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read))
            using (BinaryReader reader = new BinaryReader(fs, Encoding.UTF8))
            {
                if (fs.Length < 4) // Too tiny to even have a version number
                {
                    Debug.LogWarning($"[FileIndexIO] Index file is dead or empty at: {path}");
                    return new Dictionary<long, FileIndex>();
                }

                // Check version. If it doesn't match, we probably need a repack.
                int version = reader.ReadInt32();
                if (version != INDEX_VERSION)
                {
                    Debug.LogError($"[FileIndexIO] Version mismatch! Found: {version}, Expected: {INDEX_VERSION}. Give it a repack.");
                    return new Dictionary<long, FileIndex>();
                }

                if (fs.Position >= fs.Length) // End of file before we even started? Suspicious.
                {
                     Debug.LogWarning($"[FileIndexIO] Index file cut short at: {path}");
                     return new Dictionary<long, FileIndex>();
                }

                int count = reader.ReadInt32();

                if (DynamicSizeDictionary)
                {
                    result = new Dictionary<long, FileIndex>(); // Let it grow
                }
                else{
                    result = new Dictionary<long, FileIndex>(count); // Pre-allocate for speed
                }

                for (int i = 0; i < count; i++)
                {
                    FileIndex entry = new FileIndex();
                    
                    // Heads up: Read these in the EXACT same order they were saved!
                    entry.Id = reader.ReadInt32();
                    entry.NameHash = reader.ReadInt64();
                    entry.Offset = reader.ReadInt64();
                    entry.Length = reader.ReadInt64();
                    entry.UncompressedLength = reader.ReadInt64();
                    entry.IsEncrypted = reader.ReadBoolean();
                    entry.IsCompressed = reader.ReadBoolean(); // New in V5

                    // Map it using the hash as the key for that sweet O(1) lookup
                    if (!result.ContainsKey(entry.NameHash))
                    {
                        result.Add(entry.NameHash, entry);
                    }
                }
            }

            return result;
        }
    }
}
