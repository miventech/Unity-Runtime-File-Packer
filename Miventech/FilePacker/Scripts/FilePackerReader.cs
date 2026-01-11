using System;
using System.Collections.Generic;
using System.IO;
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
    public class FilePackerReader : IDisposable
    {
        public string FilePathIndex;
        private string DirectoryPath; // Where all the packed chunks live
        private Dictionary<long, FileIndex> FileMap;
        
        public Dictionary<string, FileStream> ChunkStreams = new Dictionary<string, FileStream>();
        
        public FilePackerReader(string IndexFile)
        {
            FilePathIndex = IndexFile;
            DirectoryPath = Path.GetDirectoryName(IndexFile);
            LoadIndex();
        }

        public void LoadIndex()
        {
            // Pull the index using our optimized binary version
            FileMap = FileIndexIO.LoadIndex(FilePathIndex);
            DirectoryPath = Path.GetDirectoryName(FilePathIndex);
            Debug.Log($"[FilePackerReader] Index loaded from: {FilePathIndex} ({FileMap.Count} items)");
            LoadChunkStream();
        }

        public bool HasFile(string fileName)
        {
            long hash = HashUtils.GetStableHash(fileName);
            return FileMap.ContainsKey(hash);
        }

        private void LoadChunkStream()
        {
            if (ChunkStreams == null) ChunkStreams = new Dictionary<string, FileStream>();
            
            CloseAllStreams(); // Clear out the old ones first

            // Scan and open all available data chunks
            if (Directory.Exists(DirectoryPath))
            {
                string indexName = Path.GetFileNameWithoutExtension(FilePathIndex);
                string[] files = Directory.GetFiles(DirectoryPath, $"{indexName}_data_*{SettingFilePacker.ExtensionChunkFile}");
                foreach (string filePath in files)
                {
                    string fileName = Path.GetFileName(filePath);
                    try
                    {
                        // Use FileShare.ReadWrite so we can read even if a Writer is currently packing more assets
                        FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                        ChunkStreams.Add(fileName, fs);
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"[FilePackerReader] Oops, couldn't open chunk {fileName}: {e.Message}");
                    }
                }
            }
        }

        public byte[] ReadFile(string fileName)
        {
            long hash = HashUtils.GetStableHash(fileName);
            
            if (!FileMap.ContainsKey(hash))
            {
                Debug.LogError($"[FilePackerReader] File not found: {fileName} (Hash: {hash})");
                return null;
            }

            FileIndex entry = FileMap[hash];
            return ReadDataFromChunk(entry);
        }

        private byte[] ReadDataFromChunk(FileIndex entry)
        {
            long chunkIndex = entry.Offset / SettingFilePacker.MAX_CHUNK_SIZE;
            long localOffset = entry.Offset % SettingFilePacker.MAX_CHUNK_SIZE;
            
            string indexName = Path.GetFileNameWithoutExtension(FilePathIndex);
            string chunkName = $"{indexName}_data_{chunkIndex}{SettingFilePacker.ExtensionChunkFile}";

            FileStream fs = GetChunkStream(chunkName);
            if (fs == null) return null;

            byte[] buffer = new byte[entry.Length];

            // Use the shared stream with a basic lock to keep it thread-safe-ish
            lock (fs)
            {
                fs.Seek(localOffset, SeekOrigin.Begin);
                
                int totalRead = 0;
                int bytesToRead = (int)entry.Length;
                while (totalRead < bytesToRead)
                {
                    int r = fs.Read(buffer, totalRead, bytesToRead - totalRead);
                    if (r <= 0) break; // End of stream reached unexpectedly
                    totalRead += r;
                }
            }

            // --- Decryption Layer ---
            if (entry.IsEncrypted)
            {
                buffer = EncryptionUtils.Decrypt(buffer, SettingFilePacker.EncryptionKey);
            }

            // --- Decompression Layer ---
            if (entry.IsCompressed)
            {
                return CompressionUtils.Decompress(buffer);
            }

            return buffer;
        }

        public Stream GetFileStream(string fileName, out long offset, out long length)
        {
            offset = 0;
            length = 0;

            long hash = HashUtils.GetStableHash(fileName);
            if (!FileMap.ContainsKey(hash)) return null;

            FileIndex entry = FileMap[hash];

            long chunkIndex = entry.Offset / SettingFilePacker.MAX_CHUNK_SIZE;
            offset = entry.Offset % SettingFilePacker.MAX_CHUNK_SIZE;
            length = entry.Length;

            string indexName = Path.GetFileNameWithoutExtension(FilePathIndex);
            string chunkName = $"{indexName}_data_{chunkIndex}{SettingFilePacker.ExtensionChunkFile}";
            
            // Return the shared stream. WARNING: Don't Dispose this from the outside!
            return GetChunkStream(chunkName);
        }

        private FileStream GetChunkStream(string chunkName)
        {
            if (ChunkStreams.TryGetValue(chunkName, out FileStream stream))
            {
                return stream;
            }

            // Lazy Load: Try to open it if it's not already in our dictionary
            string fullPath = Path.Combine(DirectoryPath, chunkName);
            if (File.Exists(fullPath))
            {
                try
                {
                    // Always use FileShare.ReadWrite to allow writing logs or updating the package while reading
                    stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    ChunkStreams[chunkName] = stream;
                    return stream;
                }
                catch (Exception e)
                {
                    Debug.LogError($"[FilePackerReader] Lazy-load failed for chunk {chunkName}: {e.Message}");
                }
            }
            
            return null;
        }

        public void CloseAllStreams()
        {
            foreach (var stream in ChunkStreams.Values)
            {
                stream.Close();
            }
            ChunkStreams.Clear();
        }

        internal string ReadFileAsString(string pathOfReader)
        {
            byte[] data = ReadFile(pathOfReader);
            if (data == null) return null;
            return System.Text.Encoding.UTF8.GetString(data);
        }

        public void Dispose()
        {
            CloseAllStreams();
        }
    

        //esta por compatibilidad con el System.File 
        internal bool Exists(string pathOfReader) //check if file exists in the packer
        {
            return HasFile(pathOfReader);
        }
    }
}