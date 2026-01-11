using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Miventech.FilePacker.Tools;
using UnityEngine;

    /*
              &&&&&&              &&&&&&              
          &&&&&&$&&&&&&        &&&&&&$&&&&&&          
       &&&&&&       ;&&&&&  &&&&&&       &&&&&&       
    &&&&&&              &&&&&&              &&&&&&    
 &&&&&&       &&&&&&       &&&&   &&&&&&       &&&&&& 
&&&&       &&&+    ;&&&       &&&&X    X&&&       &&&&
&&     .&&&.          .&&&  &&&X           &&&x     &&
&&    &&.                .&&+                 &&    &&
&&    &&...            ...&&xXX            ++ X&    &&
&&    &&.......    ..... .&&: +XXXx     XXx:  X&    &&
&&    &&................ .&&; :::;xXXXx+.     X&    &&
&&    &&......... ...... .&&; ::::::;         X&    &&
&&    &&......... .....  .&&   .::::;         X&    &&
&&    &&&........ ..    .&&&&X    .:;        X&&    &&
&&      &&&&.....    .&&&;  ;&&&X.       X&&&&      &&
&&&&       .&&&....&&&;        ;&&&X; X&&&x       &&&&
 &&&&&&&       &&&&;              ;&&&&       &&&&&&& 
     &&&&&&     &&                  &&     &&&&&&     
        &&&&&&  &&.:;;;:      .:;:. &&  &&&&&&        
         &&&&&&&&&..:::;;;;.;::.... &&&&&&&&&         
         &&&&   &&..::::::; ....... &&   ;&&&         
         &&&&   && ..:::::; ......  &&   &&&&         
         &&&&   &&;   ..::; ..     ;&&   &&&&         
         &&&&     &&&;:   :     ;&&&X    +&&&         
         &&&&&       &&&&;. .;&&&       &&&&&         
           &&&&&&       &&&&&&       &&&&&&           
              &&&&&&              &&&&&&              
                  &&&&&        &&&&&                  
                     &&&&&&&&&&&&                     
                        &&&&&&                                
    
    */
namespace Miventech.FilePacker
{

    public class FilePackerWriter : IDisposable
    {
        public string FilePathIndex;
        private string DirectoryPath; // Where the packed files hang out
        private Dictionary<long, FileIndex> FileMap;

        // --- OPTIMIZATION: Hold onto the stream so we don't open/close it constantly ---
        private FileStream _cachedStream;
        private long _cachedChunkIndex = -1;
        // -----------------------------------------------------------------------------

        // This class is the heavy lifter for writing files into the packing system.
        public FilePackerWriter(string PathFileIndex)
        {
            this.FilePathIndex = PathFileIndex;
            this.DirectoryPath = System.IO.Path.GetDirectoryName(PathFileIndex);

            // Load existing index or start fresh if it's not there
            if (File.Exists(FilePathIndex))
            {
                FileMap = FileIndexIO.LoadIndex(FilePathIndex, false);

                // Safety check: if the file exists but we got 0 items, either it's empty 
                // or there was a version mismatch/error.
                if (FileMap.Count == 0 && new FileInfo(FilePathIndex).Length > 10)
                {
                    Debug.LogError("[FilePackerWriter] Index file might be corrupted or version mismatched. " +
                                   "Aborting to prevent overwriting your data chunks!");
                    // We shouldn't proceed if we can't reliably load the existing index
                }
            }
            else
            {
                FileMap = new Dictionary<long, FileIndex>();
            }
        }

        public void AddFileToPackage(string PathFileToAdd, string NameSave, bool autoSave = true, bool compress = false, bool encrypt = false)
        {
            if (!File.Exists(PathFileToAdd))
            {
                Debug.LogError($"[FilePackerWriter] File not found: {PathFileToAdd}");
                return;
            }

            // Using the hash of the name for our lookup
            long nameHash = HashUtils.GetStableHash(NameSave);

            // Read raw bytes from the disk
            byte[] fileData = File.ReadAllBytes(PathFileToAdd);
            long originalLength = fileData.Length;

            if (compress)
            {
                fileData = CompressionUtils.Compress(fileData);
            }

            if (encrypt)
            {
                fileData = EncryptionUtils.Encrypt(fileData, SettingFilePacker.EncryptionKey);
            }

            long length = fileData.Length;

            // Figure out where to dump this data (Global Offset)
            long writeOffset = GetNextFreePosition(length);

            // Write into the binary chunks
            WriteDataToChunks(fileData, writeOffset);

            // Simple auto-increment ID hack
            int newId = (FileMap.Count > 0) ? FileMap.Values.Max(x => x.Id) + 1 : 1;

            // Build the index entry
            FileIndex newEntry = new FileIndex
            {
                Id = newId,
                NameHash = nameHash,
                Offset = writeOffset,
                Length = length,
                UncompressedLength = originalLength,
                IsEncrypted = encrypt,
                IsCompressed = compress
            };

            // Update the map. Note: if we overwrite, the old data stays as "dead space" in the chunk.
            if (FileMap.ContainsKey(nameHash))
            {
                // In a production system, you'd want a compactor to clean this up.
                FileMap[nameHash] = newEntry;
            }
            else
            {
                FileMap.Add(nameHash, newEntry);
            }

            // Dump index to disk if requested. Keep this false for batch operations!
            if (autoSave) Save();

            Debug.Log($"[FilePackerWriter] Packed: {NameSave} | Chunk: {writeOffset / SettingFilePacker.MAX_CHUNK_SIZE} | Offset: {writeOffset}");
        }

        public void AddFileToPackage(byte[] fileData, string NameSave, bool autoSave = false, bool compress = false, bool encrypt = false)
        {
            long nameHash = HashUtils.GetStableHash(NameSave);
            long originalLength = fileData.Length;

            if (compress)
            {
                fileData = CompressionUtils.Compress(fileData);
            }

            if (encrypt)
            {
                fileData = EncryptionUtils.Encrypt(fileData, SettingFilePacker.EncryptionKey);
            }

            long length = fileData.Length;
            long writeOffset = GetNextFreePosition(length);

            WriteDataToChunks(fileData, writeOffset);

            int newId = (FileMap.Count > 0) ? FileMap.Values.Max(x => x.Id) + 1 : 1;

            FileIndex newEntry = new FileIndex
            {
                Id = newId,
                NameHash = nameHash,
                Offset = writeOffset,
                Length = length,
                UncompressedLength = originalLength,
                IsEncrypted = encrypt,
                IsCompressed = compress
            };

            if (FileMap.ContainsKey(nameHash))
            {
                FileMap[nameHash] = newEntry;
            }
            else
            {
                FileMap.Add(nameHash, newEntry);
            }

            if (autoSave) Save();

            Debug.Log($"[FilePackerWriter] Packed (bytes): {NameSave} | Chunk: {writeOffset / SettingFilePacker.MAX_CHUNK_SIZE} | Offset: {writeOffset}");
        }

        public string[] GetNameChunksFiles()
        {
            string indexName = Path.GetFileNameWithoutExtension(FilePathIndex);
            string[] files = Directory.GetFiles(DirectoryPath, $"{indexName}_data_*{SettingFilePacker.ExtensionChunkFile}");
            return files;
        }

        public void removeFileFromPackage(string NameToRemove, bool autoSave = true)
        {
            long nameHash = HashUtils.GetStableHash(NameToRemove);

            if (FileMap.ContainsKey(nameHash))
            {
                FileMap.Remove(nameHash);
                if (autoSave) Save();
                Debug.Log($"[FilePackerWriter] Removed from index: {NameToRemove}");
            }
            else
            {
                Debug.LogWarning($"[FilePackerWriter] Couldn't remove, file not in index: {NameToRemove}");
            }
        }

        // Manually trigger a save. Useful after doing a big batch.
        public void Save()
        {
            CloseStream(); // Close data stream before saving the index metadata
            FileIndexIO.SaveIndex(FilePathIndex, FileMap);
        }

        // Find the next available spot, skipping cross-chunk boundaries.
        private long GetNextFreePosition(long lengthToAdd)
        {
            if (FileMap.Count == 0) return 0;

            // Look for the end of the last written file
            long maxEndPosition = 0;
            foreach (var entry in FileMap.Values)
            {
                long end = entry.Offset + entry.Length;
                if (end > maxEndPosition) maxEndPosition = end;
            }

            // Check if we're bleeding into the next chunk
            long currentChunk = maxEndPosition / SettingFilePacker.MAX_CHUNK_SIZE;
            long endChunk = (maxEndPosition + lengthToAdd - 1) / SettingFilePacker.MAX_CHUNK_SIZE;

            if (currentChunk != endChunk)
            {
                // If it crosses, we jump to the start of the next chunk. 
                // A bit of wasted space, but makes reading way smoother.
                return (currentChunk + 1) * SettingFilePacker.MAX_CHUNK_SIZE;
            }

            return maxEndPosition;
        }

        public void RemoveExistingChunks()
        {
            CloseStream(); // Always close before deleting
            string indexName = Path.GetFileNameWithoutExtension(FilePathIndex);
            string[] files = Directory.GetFiles(DirectoryPath, $"{indexName}_data_*{SettingFilePacker.ExtensionChunkFile}");
            foreach (string file in files)
            {
                File.Delete(file);
            }
        }

        public void ClearIndex()
        {
            CloseStream();
            FileMap.Clear();
            Save();
        }

        private void WriteDataToChunks(byte[] data, long globalOffset)
        {
            // Map global offset to chunk ID + local offset
            long chunkIndex = globalOffset / SettingFilePacker.MAX_CHUNK_SIZE;
            long localOffset = globalOffset % SettingFilePacker.MAX_CHUNK_SIZE;

            // If we moved to a new chunk (or just started), rotate the stream
            if (_cachedStream == null || _cachedChunkIndex != chunkIndex)
            {
                CloseStream();

                string indexName = Path.GetFileNameWithoutExtension(FilePathIndex);
                string chunkPath = Path.Combine(DirectoryPath, $"{indexName}_data_{chunkIndex}{SettingFilePacker.ExtensionChunkFile}");

                if (!Directory.Exists(DirectoryPath)) Directory.CreateDirectory(DirectoryPath);

                // Keep it open for business
                _cachedStream = new FileStream(chunkPath, FileMode.OpenOrCreate, FileAccess.Write, FileShare.Read);
                _cachedChunkIndex = chunkIndex;

                // Jump to the end for safety
                _cachedStream.Seek(0, SeekOrigin.End);
            }

            // If the local offset doesn't match where we are, jump to the right spot
            if (_cachedStream.Position != localOffset)
            {
                _cachedStream.Seek(localOffset, SeekOrigin.Begin);
            }

            _cachedStream.Write(data, 0, data.Length);
        }

        private void CloseStream()
        {
            if (_cachedStream != null)
            {
                _cachedStream.Flush();
                _cachedStream.Close();
                _cachedStream.Dispose();
                _cachedStream = null;
                _cachedChunkIndex = -1;
            }
        }

        public void Dispose()
        {
            CloseStream();
        }
    }
}