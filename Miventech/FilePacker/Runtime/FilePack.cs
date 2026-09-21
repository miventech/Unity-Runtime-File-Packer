using System;
using System.Collections.Generic;
using System.IO;
using Miventech.FilePacker.Tools;

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
    /// API fluida de ESCRITURA de paquetes FilePacker v6.
    /// Reemplaza a FilePackerWriter: si el índice existe se carga (nunca se
    /// sobrescribe un índice ilegible) y Dispose hace auto-guardado.
    ///
    /// Uso:
    ///   using (FilePack.Create(path, new PackOptions { Codec = EntryCodecMode.Lz4, Encrypt = true, Password = "secreta" }))
    ///   {
    ///       pack.AddFile(ruta, "nivel1.json").AddData(bytes, "textura.bin");
    ///   } // Dispose → Save automático
    /// </summary>
    public sealed class FilePack : IDisposable
    {
        private const byte EncryptedFlag = 1;

        private readonly string _indexPath;
        private readonly string _directoryPath;
        private readonly PackOptions _options;
        private readonly PackIndexData _index;
        private readonly Dictionary<string, PackEntry> _byName;
        private readonly PackCrypto _crypto;
        private FileStream _cachedStream;
        private long _cachedChunkIndex = -1;
        private long _nextWritePosition;
        private int _nextId;

        private FilePack(string indexPath, PackOptions options, PackIndexData index)
        {
            _indexPath = indexPath;
            _directoryPath = Path.GetDirectoryName(indexPath);
            _options = options ?? new PackOptions();
            _index = index;
            _byName = new Dictionary<string, PackEntry>(StringComparer.Ordinal);

            foreach (PackEntry entry in _index.Entries)
            {
                _byName[HashUtils.NormalizeName(entry.Name)] = entry;
                long end = entry.Offset + entry.Length;
                if (end > _nextWritePosition) _nextWritePosition = end;
                if (entry.Id >= _nextId) _nextId = entry.Id + 1;
            }

            if (_options.Encrypt && !_index.IsEncrypted)
            {
                if (string.IsNullOrEmpty(_options.Password)) throw new ArgumentException("[FilePack] PackOptions.Encrypt requiere una Password.", "PackOptions.Password");
                byte[] masterKey = PackCrypto.GenerateMasterKey();
                _index.MasterKeyBlob = PackCrypto.ProtectMasterKey(masterKey, _options.Password, _options.KdfIterations);
                _crypto = new PackCrypto(masterKey);
            }

            if (_crypto == null && _index.IsEncrypted)
            {
                if (string.IsNullOrEmpty(_options.Password)) throw new FilePackerException("[FilePack] El paquete está cifrado: indica la Password en PackOptions.");
                try
                {
                    _crypto = new PackCrypto(PackCrypto.UnprotectMasterKey(_index.MasterKeyBlob, _options.Password));
                }
                catch (Exception e)
                {
                    throw new FilePackerException("[FilePack] Password incorrecta o sobre de la clave maestra corrupto.", e);
                }
            }
        }

        /// <summary>
        /// Abre (o crea) un paquete para escribir. Si el índice existente está
        /// corrupto lanza PackCorruptIndexException en vez de sobrescribirlo.
        /// </summary>
        public static FilePack Create(string indexPath, PackOptions options = null)
        {
            PackIndexData index;
            if (File.Exists(indexPath))
            {
                if (!PackIndexIO.TryLoad(indexPath, out index))
                {
                    throw new PackCorruptIndexException("[FilePack] Índice ilegible o de otra versión: " + indexPath + ". No se sobrescribirá para proteger los chunks existentes.");
                }
            }
            else
            {
                index = new PackIndexData();
            }
            return new FilePack(indexPath, options, index);
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

        /// <summary>Añade un archivo del disco al paquete.</summary>
        public FilePack AddFile(string path, string name)
        {
            if (!File.Exists(path)) throw new FileNotFoundException("[FilePack] Archivo no encontrado: " + path, path);
            return AddData(File.ReadAllBytes(path), name);
        }

        /// <summary>Añade datos en memoria al paquete. Sobrescribir un nombre deja espacio muerto en el chunk (sin compactación).</summary>
        public FilePack AddData(byte[] data, string name, EntryCodecMode? codec = null, bool? encrypt = null)
        {
            if (data == null) throw new ArgumentNullException("data");
            string normalized = HashUtils.NormalizeName(name);
            if (string.IsNullOrEmpty(normalized)) throw new ArgumentException("[FilePack] Nombre de entrada vacío.", "name");

            EntryCodecMode effectiveCodec = codec ?? _options.Codec;
            bool effectiveEncrypt = encrypt ?? _options.Encrypt;
            if (effectiveEncrypt && _crypto == null) throw new InvalidOperationException("[FilePack] El paquete se abrió sin cifrado; no se puede cifrar esta entrada.");

            uint checksum = Crc32.Compute(data);
            byte[] payload = EntryCodec.Compress(data, effectiveCodec);
            if (effectiveEncrypt) payload = _crypto.EncryptEntry(payload);

            long position = ReservePosition(payload.Length);
            WriteToChunks(payload, position);

            PackEntry oldEntry;
            if (_byName.TryGetValue(normalized, out oldEntry)) _index.Entries.Remove(oldEntry);

            var entry = new PackEntry
            {
                Id = _nextId++,
                NameHash = HashUtils.GetStableHash(normalized),
                Name = normalized,
                Offset = position,
                Length = payload.Length,
                UncompressedLength = data.Length,
                Flags = effectiveEncrypt ? EncryptedFlag : (byte)0,
                Codec = (byte)effectiveCodec,
                Checksum = checksum,
            };
            _index.Entries.Add(entry);
            _byName[normalized] = entry;
            _nextWritePosition = position + payload.Length;
            return this;
        }

        /// <summary>Quita una entrada del índice (los bytes quedan como espacio muerto hasta repack). Devuelve false si no existía.</summary>
        public bool TryRemove(string name)
        {
            string normalized = HashUtils.NormalizeName(name);
            PackEntry entry;
            if (!_byName.TryGetValue(normalized, out entry)) return false;
            _index.Entries.Remove(entry);
            _byName.Remove(normalized);
            return true;
        }

        public string[] GetFileNames()
        {
            var names = new List<string>(_byName.Keys);
            names.Sort(StringComparer.Ordinal);
            return names.ToArray();
        }

        public void Save()
        {
            CloseChunkStream();
            PackIndexIO.Save(_indexPath, _index);
        }

        private long ReservePosition(long length)
        {
            long position = _nextWritePosition;
            if (length <= 0) return position;
            long currentChunk = position / PackSettings.MaxChunkSize;
            long endChunk = (position + length - 1) / PackSettings.MaxChunkSize;
            if (currentChunk != endChunk)
            {
                // Cruza el límite de chunk: salta al inicio del siguiente (espacio muerto deliberado)
                position = (currentChunk + 1) * PackSettings.MaxChunkSize;
            }
            return position;
        }

        private void WriteToChunks(byte[] payload, long globalOffset)
        {
            long chunkIndex = globalOffset / PackSettings.MaxChunkSize;
            long localOffset = globalOffset % PackSettings.MaxChunkSize;

            if (_cachedStream == null || _cachedChunkIndex != chunkIndex)
            {
                CloseChunkStream();
                if (!string.IsNullOrEmpty(_directoryPath) && !Directory.Exists(_directoryPath)) Directory.CreateDirectory(_directoryPath);
                _cachedStream = new FileStream(ChunkPath(chunkIndex), FileMode.OpenOrCreate, FileAccess.Write, FileShare.Read);
                _cachedChunkIndex = chunkIndex;
            }

            _cachedStream.Seek(localOffset, SeekOrigin.Begin);
            _cachedStream.Write(payload, 0, payload.Length);
        }

        private string ChunkPath(long chunkIndex)
        {
            return Path.Combine(_directoryPath, PackSettings.GetChunkFileName(_indexPath, chunkIndex));
        }

        private void CloseChunkStream()
        {
            if (_cachedStream == null) return;
            _cachedStream.Flush();
            _cachedStream.Dispose();
            _cachedStream = null;
            _cachedChunkIndex = -1;
        }

        public void Dispose()
        {
            Save();
        }
    }
}
