using System;
using System.IO;
using System.Text;
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
&&    &&......... ...... .&&   .::::;         X&    &&
&&    &&......... .....  .&&   .::::;         X&    &&
&&    &&&........ ..    .&&&&X    .:;        X&&    &&
&&      &&&&.....    .&&&;  ;&&&X;       X&&&&      &&
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
    /// <summary>
    /// Serialización binaria del índice (formato v6 "MVPI").
    /// A diferencia de la v1: TryLoad no crea archivos como efecto secundario,
    /// valida magic y versión, y no descarta entradas duplicadas en silencio.
    /// </summary>
    public static class PackIndexIO
    {
        private const int IndexVersion = 6;
        private static readonly byte[] Magic = { (byte)'M', (byte)'V', (byte)'P', (byte)'I' };

        public static void Save(string path, PackIndexData index)
        {
            if (index == null) throw new ArgumentNullException(nameof(index));

            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory)) Directory.CreateDirectory(directory);

            using (FileStream fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))
            using (BinaryWriter writer = new BinaryWriter(fs, Encoding.UTF8))
            {
                writer.Write(Magic);
                writer.Write(IndexVersion);
                writer.Write(index.IsEncrypted);
                if (index.IsEncrypted)
                {
                    writer.Write(index.MasterKeyBlob.Length);
                    writer.Write(index.MasterKeyBlob);
                }
                writer.Write(index.Entries.Count);
                for (int i = 0; i < index.Entries.Count; i++)
                {
                    PackEntry entry = index.Entries[i];
                    writer.Write(entry.Id);
                    writer.Write(entry.NameHash);
                    writer.Write(entry.Name);
                    writer.Write(entry.Offset);
                    writer.Write(entry.Length);
                    writer.Write(entry.UncompressedLength);
                    writer.Write(entry.Flags);
                    writer.Write(entry.Codec);
                    writer.Write(entry.Checksum);
                }
            }
        }

        /// <summary>
        /// Carga el índice. Devuelve false si no existe, está corrupto o es de otra
        /// versión. NUNCA crea el archivo ni devuelve un índice a medias.
        /// </summary>
        public static bool TryLoad(string path, out PackIndexData index)
        {
            index = null;
            if (!File.Exists(path)) return false;

            try
            {
                using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (BinaryReader reader = new BinaryReader(fs, Encoding.UTF8))
                {
                    if (fs.Length < 4) return false;

                    byte[] magic = reader.ReadBytes(4);
                    if (magic.Length != 4 || magic[0] != Magic[0] || magic[1] != Magic[1] || magic[2] != Magic[2] || magic[3] != Magic[3])
                    {
                        Debug.LogWarning("[FilePacker] Magic inválido en el índice (¿paquete v1 v5?). Repaqueta para migrar a v6: " + path);
                        return false;
                    }

                    int version = reader.ReadInt32();
                    if (version != IndexVersion)
                    {
                        Debug.LogWarning("[FilePacker] Versión de índice no soportada: " + version + " (esperada " + IndexVersion + "). Repaqueta con la v2.");
                        return false;
                    }

                    bool encrypted = reader.ReadBoolean();
                    var data = new PackIndexData();
                    if (encrypted)
                    {
                        int blobLength = reader.ReadInt32();
                        if (blobLenInvalid(blobLength, fs.Length)) return false;
                        data.MasterKeyBlob = reader.ReadBytes(blobLength);
                        if (data.MasterKeyBlob.Length != blobLength) return false;
                    }

                    int count = reader.ReadInt32();
                    if (count < 0 || count > 100_000_000) return false;
                    data.Entries.Capacity = count;
                    for (int i = 0; i < count; i++)
                    {
                        var entry = new PackEntry();
                        // Leer en EXACTAMENTE el mismo orden en que se guardó
                        entry.Id = reader.ReadInt32();
                        entry.NameHash = reader.ReadInt64();
                        entry.Name = reader.ReadString();
                        entry.Offset = reader.ReadInt64();
                        entry.Length = reader.ReadInt64();
                        entry.UncompressedLength = reader.ReadInt64();
                        entry.Flags = reader.ReadByte();
                        entry.Codec = reader.ReadByte();
                        entry.Checksum = reader.ReadUInt32();
                        data.Entries.Add(entry);
                    }
                    index = data;
                    return true;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[FilePacker] Índice corrupto: " + path + " (" + e.Message + ")");
                return false;
            }
        }

        private static bool blobLenInvalid(int blobLength, long streamLength)
        {
            return blobLength < 0 || blobLength > streamLength;
        }
    }
}
