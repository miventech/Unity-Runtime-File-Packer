using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
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
    /// Lectura de paquetes FilePacker v6. Reemplaza a FilePackerReader:
    /// TryReadFile sin nulls mágicos, listado del contenido, verificación de
    /// checksum CRC32 al leer, streams con descifrado on-the-fly y API async.
    ///
    ///   using (var reader = new PackReader(path, "secreta"))
    ///   {
    ///       byte[] data = reader.ReadFile("nivel1.json");
    ///       string json = reader.ReadText("nivel1.json");
    ///       byte[] async = await reader.ReadFileAsync("textura.bin");
    ///       Stream stream = reader.OpenRead("video.dat");
    ///   }
    /// </summary>
    public sealed class PackReader : IDisposable
    {
        private readonly string _indexPath;
        private readonly string _directoryPath;
        private readonly PackIndexData _index;
        private readonly Dictionary<string, PackEntry> _byName;
        private readonly PackCrypto _crypto;
        private readonly Dictionary<long, FileStream> _chunkStreams = new Dictionary<long, FileStream>();

        public PackReader(string indexPath, string password = null)
        {
            _indexPath = indexPath;
            _directoryPath = Path.GetDirectoryName(indexPath);

            PackIndexData index;
            if (!PackIndexIO.TryLoad(indexPath, out index))
            {
                throw new PackCorruptIndexException("[PackReader] No se pudo cargar el índice: " + indexPath);
            }
            _index = index;

            _byName = new Dictionary<string, PackEntry>(StringComparer.Ordinal);
            foreach (PackEntry entry in _index.Entries)
            {
                _byName[HashUtils.NormalizeName(entry.Name)] = entry;
            }

            if (_index.IsEncrypted)
            {
                if (string.IsNullOrEmpty(password)) throw new FilePackerException("[PackReader] El paquete está cifrado: pasa la password al constructor.");
                try
                {
                    _crypto = new PackCrypto(PackCrypto.UnprotectMasterKey(_index.MasterKeyBlob, password));
                }
                catch (Exception e)
                {
                    throw new FilePackerException("[PackReader] Password incorrecta o sobre de la clave maestra corrupto.", e);
                }
            }
            else
            {
                _crypto = null;
            }
        }

        public string IndexPath
        {
            get
            {
                return _indexPath;
            }
        }

        public int EntryCount
        {
            get
            {
                return _index.Entries.Count;
            }
        }

        /// <summary>Lista los nombres (normalizados) de todas las entradas del paquete.</summary>
        public string[] ListFiles()
        {
            var names = new string[_index.Entries.Count];
            for (int i = 0; i < names.Length; i++)
            {
                names[i] = _index.Entries[i].Name;
            }
            Array.Sort(names, StringComparer.Ordinal);
            return names;
        }

        /// <summary>
        /// Lista recursivamente las entradas dentro de una carpeta del paquete.
        /// "" o "/" = raíz (equivale a ListFiles()). Ejemplo: "datos", "texturas/ui".
        /// </summary>
        public string[] ListFiles(string folder)
        {
            string folderPath = HashUtils.NormalizeName(folder).TrimEnd('/');
            var names = new List<string>();
            for (int i = 0; i < _index.Entries.Count; i++)
            {
                string name = HashUtils.NormalizeName(_index.Entries[i].Name);
                if (folderPath.Length == 0 || name.StartsWith(folderPath + "/", StringComparison.Ordinal))
                {
                    names.Add(name);
                }
            }
            names.Sort(StringComparer.Ordinal);
            return names.ToArray();
        }

        /// <summary>Árbol de jerarquía (carpetas y archivos) del contenido del paquete.</summary>
        public PackNode GetHierarchy()
        {
            return PackHierarchy.Build(_index.Entries);
        }

        public bool Contains(string name)
        {
            return _byName.ContainsKey(HashUtils.NormalizeName(name));
        }

        /// <summary>
        /// Lee una entrada. False solo si NO existe; si los datos están corruptos lanza PackIntegrityException.
        /// </summary>
        public bool TryReadFile(string name, out byte[] data)
        {
            PackEntry entry;
            if (!TryGetEntry(name, out entry))
            {
                data = null;
                return false;
            }
            data = ReadEntry(entry);
            return true;
        }

        /// <summary>Lee una entrada completa (descifra, descomprime y verifica CRC32).</summary>
        public byte[] ReadFile(string name)
        {
            return ReadEntry(GetEntryOrThrow(name));
        }

        public string ReadText(string name)
        {
            return Encoding.UTF8.GetString(ReadFile(name));
        }

        public bool TryReadText(string name, out string text)
        {
            byte[] data;
            if (!TryReadFile(name, out data))
            {
                text = null;
                return false;
            }
            text = Encoding.UTF8.GetString(data);
            return true;
        }

        /// <summary>Lectura async: la pipeline (IO + descifrado + descompresión) corre en background.</summary>
        public Task<byte[]> ReadFileAsync(string name)
        {
            return Task.Run(() => ReadFile(name));
        }

        public Task<string> ReadTextAsync(string name)
        {
            return Task.Run(() => ReadText(name));
        }

        /// <summary>
        /// Stream de solo lectura con descifrado/descompresión on-the-fly.
        /// Soporta None y Deflate en streaming (LZ4 requiere el bloque completo).
        /// Verifica el HMAC al llegar al final del stream si la entrada está cifrada.
        /// </summary>
        public Stream OpenRead(string name)
        {
            return new EntryStream(this, GetEntryOrThrow(name), _crypto);
        }

        /// <summary>Verifica el CRC32 de todas las entradas del paquete. Devuelve true si todo está íntegro.</summary>
        public bool VerifyAll()
        {
            foreach (PackEntry entry in _index.Entries)
            {
                try
                {
                    ReadEntry(entry);
                }
                catch (Exception e)
                {
                    Debug.LogError("[PackReader] Integridad fallida en '" + entry.Name + "': " + e.Message);
                    return false;
                }
            }
            return true;
        }

        private byte[] ReadEntry(PackEntry entry)
        {
            if (entry.Length > int.MaxValue) throw new NotSupportedException("[PackReader] Entrada > 2GB no soportada en lectura completa: " + entry.Name);

            byte[] stored = ReadStored(entry);
            byte[] plain = entry.IsEncrypted ? _crypto.DecryptEntry(stored) : stored;
            byte[] data = EntryCodec.Decompress(plain, entry.CodecMode, (int)entry.UncompressedLength);

            if (Crc32.Compute(data) != entry.Checksum)
            {
                throw new PackIntegrityException("[PackReader] Checksum CRC32 inválido en '" + entry.Name + "': paquete corrupto.");
            }
            return data;
        }

        private byte[] ReadStored(PackEntry entry)
        {
            long chunkIndex = entry.Offset / PackSettings.MaxChunkSize;
            long localOffset = entry.Offset % PackSettings.MaxChunkSize;

            FileStream fs;
            try
            {
                fs = AcquireChunkStream(chunkIndex);
            }
            catch (FileNotFoundException e)
            {
                throw new PackFileNotFoundException("[PackReader] Chunk faltante para '" + entry.Name + "': " + e.FileName);
            }

            byte[] buffer = new byte[entry.Length];
            lock (fs)
            {
                fs.Seek(localOffset, SeekOrigin.Begin);
                int total = 0;
                int remaining = (int)entry.Length;
                while (remaining > 0)
                {
                    int read = fs.Read(buffer, total, remaining);
                    if (read <= 0) throw new PackIntegrityException("[PackReader] Chunk truncado al leer '" + entry.Name + "'.");
                    total += read;
                    remaining -= read;
                }
            }
            return buffer;
        }

        internal FileStream AcquireChunkStream(long chunkIndex)
        {
            FileStream stream;
            if (_chunkStreams.TryGetValue(chunkIndex, out stream)) return stream;

            string chunkPath = Path.Combine(_directoryPath, PackSettings.GetChunkFileName(_indexPath, chunkIndex));
            // FileShare.ReadWrite permite leer mientras un FilePack escribe más entradas
            stream = new FileStream(chunkPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            _chunkStreams[chunkIndex] = stream;
            return stream;
        }

        private bool TryGetEntry(string name, out PackEntry entry)
        {
            return _byName.TryGetValue(HashUtils.NormalizeName(name), out entry);
        }

        private PackEntry GetEntryOrThrow(string name)
        {
            string normalized = HashUtils.NormalizeName(name);
            PackEntry entry;
            if (!_byName.TryGetValue(normalized, out entry))
            {
                throw new PackFileNotFoundException("[PackReader] La entrada no existe en el paquete: " + normalized);
            }
            return entry;
        }

        public void Dispose()
        {
            foreach (FileStream stream in _chunkStreams.Values)
            {
                stream.Dispose();
            }
            _chunkStreams.Clear();
        }
    }
}
