using System;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;

namespace Miventech.FilePacker
{
    /// <summary>
    /// Stream de solo lectura que descifra y descomprime una entrada del paquete
    /// on-the-fly, sin materializar el archivo completo en memoria.
    /// - Cifrado: verifica el HMAC de forma incremental al consumir el stream.
    /// - Streaming soportado: None y Deflate. LZ4 requiere bloque completo (usa ReadFile).
    /// - El FileStream del chunk es compartido con el PackReader: este stream NO lo cierra.
    /// </summary>
    public sealed class EntryStream : Stream
    {
        private readonly Stream _root;
        private readonly Stream _innerWrapper;
        private readonly Stream _outerWrapper;
        private bool _disposed;

        internal EntryStream(PackReader reader, PackEntry entry, PackCrypto crypto)
        {
            if (entry.IsEncrypted && crypto == null) throw new FilePackerException("[EntryStream] Entrada cifrada pero el lector no tiene clave.");

            long chunkIndex = entry.Offset / PackSettings.MaxChunkSize;
            long localOffset = entry.Offset % PackSettings.MaxChunkSize;
            FileStream chunk = reader.AcquireChunkStream(chunkIndex);
            chunk.Seek(localOffset, SeekOrigin.Begin);

            Stream source = chunk;
            _innerWrapper = null;
            _outerWrapper = null;

            if (entry.IsEncrypted)
            {
                int cipherLength = (int)(entry.Length - PackCrypto.IvSize - PackCrypto.MacSize);
                byte[] iv = new byte[PackCrypto.IvSize];
                ReadExactly(chunk, iv, PackCrypto.IvSize);

                var macStream = new MacVerifyingStream(chunk, crypto, iv, cipherLength, localOffset + entry.Length - PackCrypto.MacSize);
                source = macStream;
                if (cipherLength > 0)
                {
                    var cryptoStream = new CryptoStream(source, crypto.CreateDecryptor(iv), CryptoStreamMode.Read, leaveOpen: true);
                    _innerWrapper = cryptoStream;
                    source = cryptoStream;
                }
                if (cipherLength == 0) macStream.ForceVerify();
            }
            else if (entry.Length > 0)
            {
                source = new LimitedReadStream(chunk, entry.Length);
            }

            if (entry.CodecMode == EntryCodecMode.Deflate)
            {
                var deflate = new DeflateStream(source, CompressionMode.Decompress, leaveOpen: true);
                _outerWrapper = deflate;
                _root = deflate;
            }
            else
            {
                _root = source;
            }
        }

        public override bool CanRead
        {
            get
            {
                return true;
            }
        }

        public override bool CanSeek
        {
            get
            {
                return false;
            }
        }

        public override bool CanWrite
        {
            get
            {
                return false;
            }
        }

        public override long Length
        {
            get
            {
                throw new NotSupportedException("[EntryStream] Longitud virtual no disponible en streaming.");
            }
        }

        public override long Position
        {
            get
            {
                throw new NotSupportedException("[EntryStream] No soporta Position.");
            }
            set
            {
                throw new NotSupportedException("[EntryStream] Stream de solo lectura y no seekable.");
            }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (_disposed) throw new ObjectDisposedException("EntryStream");
            return _root.Read(buffer, offset, count);
        }

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException("[EntryStream] No soporta Seek.");
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException("[EntryStream] No soporta SetLength.");
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException("[EntryStream] Stream de solo lectura.");
        }

        protected override void Dispose(bool disposing)
        {
            if (_disposed) return;
            _disposed = true;
            if (_outerWrapper != null) _outerWrapper.Dispose();
            if (_innerWrapper != null) _innerWrapper.Dispose();
            base.Dispose(disposing);
        }

        private static void ReadExactly(FileStream source, byte[] buffer, int count)
        {
            int total = 0;
            while (total < count)
            {
                int read = source.Read(buffer, total, count - total);
                if (read <= 0) throw new PackIntegrityException("[EntryStream] Chunk truncado: faltan datos de la entrada.");
                total += read;
            }
        }

        /// <summary>Lee del chunk limitado a una ventana y verifica el HMAC al consumirla completa.</summary>
        private sealed class MacVerifyingStream : Stream
        {
            private readonly FileStream _baseStream;
            private readonly HMACSHA256 _hmac;
            private readonly byte[] _expectedMac;
            private readonly long _length;
            private long _read;
            private bool _finished;

            public MacVerifyingStream(FileStream baseStream, PackCrypto crypto, byte[] iv, long length, long macPosition)
            {
                _baseStream = baseStream;
                _length = length;

                long restore = baseStream.Position;
                byte[] mac = new byte[PackCrypto.MacSize];
                baseStream.Seek(macPosition, SeekOrigin.Begin);
                ReadFully(baseStream, mac);
                baseStream.Seek(restore, SeekOrigin.Begin);
                _expectedMac = mac;

                _hmac = crypto.BeginMac(iv);
                if (_length == 0) ForceVerify();
            }

            public override bool CanRead
            {
                get
                {
                    return true;
                }
            }

            public override bool CanSeek
            {
                get
                {
                    return false;
                }
            }

            public override bool CanWrite
            {
                get
                {
                    return false;
                }
            }

            public override long Length
            {
                get
                {
                    return _length;
                }
            }

            public override long Position
            {
                get
                {
                    return _read;
                }
                set
                {
                    throw new NotSupportedException();
                }
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                if (_read >= _length) return 0;
                int toRead = (int)Math.Min(count, _length - _read);
                int read = _baseStream.Read(buffer, offset, toRead);
                if (read <= 0) throw new PackIntegrityException("[EntryStream] Chunk truncado durante el streaming.");
                _hmac.TransformBlock(buffer, offset, read, null, 0);
                _read += read;
                if (_read >= _length) ForceVerify();
                return read;
            }

            public void ForceVerify()
            {
                if (_finished) return;
                _hmac.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
                if (!PackCrypto.FixedTimeEquals(_expectedMac, _hmac.Hash))
                {
                    throw new PackIntegrityException("[EntryStream] HMAC inválido: entrada corrupta o manipulada.");
                }
                _finished = true;
            }

            private static void ReadFully(FileStream source, byte[] buffer)
            {
                int total = 0;
                while (total < buffer.Length)
                {
                    int read = source.Read(buffer, total, buffer.Length - total);
                    if (read <= 0) throw new PackIntegrityException("[EntryStream] Chunk truncado: no se pudo leer el HMAC de la entrada.");
                    total += read;
                }
            }

            public override void Flush()
            {
            }

            public override long Seek(long offset, SeekOrigin origin)
            {
                throw new NotSupportedException();
            }

            public override void SetLength(long value)
            {
                throw new NotSupportedException();
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
                throw new NotSupportedException();
            }

            protected override void Dispose(bool disposing)
            {
                // NO cierra el FileStream compartido del PackReader
                if (_hmac != null) _hmac.Dispose();
                base.Dispose(disposing);
            }
        }

        /// <summary>Ventana de lectura limitada dentro del chunk (entradas sin cifrar).</summary>
        private sealed class LimitedReadStream : Stream
        {
            private readonly FileStream _baseStream;
            private readonly long _length;
            private long _read;

            public LimitedReadStream(FileStream baseStream, long length)
            {
                _baseStream = baseStream;
                _length = length;
            }

            public override bool CanRead
            {
                get
                {
                    return true;
                }
            }

            public override bool CanSeek
            {
                get
                {
                    return false;
                }
            }

            public override bool CanWrite
            {
                get
                {
                    return false;
                }
            }

            public override long Length
            {
                get
                {
                    return _length;
                }
            }

            public override long Position
            {
                get
                {
                    return _read;
                }
                set
                {
                    throw new NotSupportedException();
                }
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                if (_read >= _length) return 0;
                int toRead = (int)Math.Min(count, _length - _read);
                int read = _baseStream.Read(buffer, offset, toRead);
                _read += Math.Max(read, 0);
                return read;
            }

            public override void Flush()
            {
            }

            public override long Seek(long offset, SeekOrigin origin)
            {
                throw new NotSupportedException();
            }

            public override void SetLength(long value)
            {
                throw new NotSupportedException();
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
                throw new NotSupportedException();
            }

            protected override void Dispose(bool disposing)
            {
                // NO cierra el FileStream compartido del PackReader
                base.Dispose(disposing);
            }
        }
    }
}
