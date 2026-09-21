using System;
using System.Security.Cryptography;
using Miventech.Security;
using UnityEngine;

namespace Miventech.FilePacker
{
    /// <summary>
    /// Cifrado del paquete usando EasyCrypto (AES-Unity, Miventech.Security) como sobre:
    /// la clave maestra (64 bytes aleatorios) se protege con EasyCrypto
    /// (AES-256-CBC + HMAC-SHA256 + PBKDF2 con salt aleatorio). Así el KDF caro
    /// corre UNA sola vez por paquete, y cada entrada se cifra/descifra rápido
    /// con AES-CBC + IV aleatorio + HMAC-SHA256 (encrypt-then-MAC).
    /// </summary>
    public sealed class PackCrypto
    {
        private const int MasterKeySize = 64; // 32 bytes AES-256 + 32 bytes HMAC
        internal const int IvSize = 16;
        internal const int MacSize = 32;

        private readonly byte[] _aesKey;
        private readonly byte[] _hmacKey;

        public static byte[] GenerateMasterKey()
        {
            byte[] masterKey = new byte[MasterKeySize];
            using (RandomNumberGenerator rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(masterKey);
            }
            return masterKey;
        }

        public static byte[] ProtectMasterKey(byte[] masterKey, string password, int iterations)
        {
            if (masterKey == null || masterKey.Length != MasterKeySize) throw new ArgumentException("[FilePacker] Clave maestra inválida.", nameof(masterKey));
            if (string.IsNullOrEmpty(password)) throw new ArgumentException("[FilePacker] Password vacía.", nameof(password));
            int previous = EasyCrypto.Iterations;
            try
            {
                EasyCrypto.Iterations = Math.Max(1000, iterations);
                return EasyCrypto.Encrypt(masterKey, password);
            }
            finally
            {
                EasyCrypto.Iterations = previous;
            }
        }

        public static byte[] UnprotectMasterKey(byte[] blob, string password)
        {
            if (blob == null || blob.Length == 0) throw new ArgumentException("[FilePacker] Sobre de clave maestra vacío.", nameof(blob));
            if (string.IsNullOrEmpty(password)) throw new ArgumentException("[FilePacker] Password vacía.", nameof(password));
            return EasyCrypto.Decrypt(blob, password); // EasyCryptoException si la clave es incorrecta
        }

        public PackCrypto(byte[] masterKey)
        {
            if (masterKey == null || masterKey.Length != MasterKeySize) throw new ArgumentException("[FilePacker] Clave maestra inválida.", nameof(masterKey));
            _aesKey = new byte[32];
            _hmacKey = new byte[32];
            Buffer.BlockCopy(masterKey, 0, _aesKey, 0, 32);
            Buffer.BlockCopy(masterKey, 32, _hmacKey, 0, 32);
        }

        /// <summary>
        /// Cifra una entrada: [IV 16][ciphertext][HMAC-SHA256 32].
        /// </summary>
        public byte[] EncryptEntry(byte[] plaintext)
        {
            if (plaintext == null) plaintext = Array.Empty<byte>();
            byte[] iv = new byte[IvSize];
            using (RandomNumberGenerator rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(iv);
            }
            byte[] cipherText = EncryptAesCbc(plaintext, iv);
            byte[] mac = ComputeMac(iv, cipherText);
            byte[] result = new byte[IvSize + cipherText.Length + MacSize];
            Buffer.BlockCopy(iv, 0, result, 0, IvSize);
            Buffer.BlockCopy(cipherText, 0, result, IvSize, cipherText.Length);
            Buffer.BlockCopy(mac, 0, result, IvSize + cipherText.Length, MacSize);
            return result;
        }

        /// <summary>
        /// Descifra una entrada verificando primero el HMAC.
        /// </summary>
        public byte[] DecryptEntry(byte[] stored)
        {
            if (stored == null || stored.Length < IvSize + MacSize) throw new PackIntegrityException("[FilePacker] Datos cifrados corruptos (longitud insuficiente).");
            int cipherLength = stored.Length - IvSize - MacSize;
            byte[] iv = new byte[IvSize];
            Buffer.BlockCopy(stored, 0, iv, 0, IvSize);
            byte[] cipherText = new byte[cipherLength];
            Buffer.BlockCopy(stored, IvSize, cipherText, 0, cipherLength);
            byte[] mac = new byte[MacSize];
            Buffer.BlockCopy(stored, IvSize + cipherLength, mac, 0, MacSize);

            byte[] expectedMac = ComputeMac(iv, cipherText);
            if (!FixedTimeEquals(mac, expectedMac)) throw new PackIntegrityException("[FilePacker] HMAC inválido: entrada corrupta o manipulada.");
            return DecryptAesCbc(cipherText, iv);
        }

        /// <summary>Descifrador para streaming (EntryStream).</summary>
        public ICryptoTransform CreateDecryptor(byte[] iv)
        {
            Aes aes = Aes.Create();
            aes.Key = _aesKey;
            aes.IV = iv;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;
            return aes.CreateDecryptor();
        }

        /// <summary>HMAC incremental iniciado con el IV (para EntryStream).</summary>
        public HMACSHA256 BeginMac(byte[] iv)
        {
            var hmac = new HMACSHA256(_hmacKey);
            hmac.TransformBlock(iv, 0, IvSize, null, 0);
            return hmac;
        }

        public byte[] FinishMac(HMACSHA256 hmac)
        {
            hmac.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
            return hmac.Hash;
        }

        internal static bool FixedTimeEquals(byte[] a, byte[] b)
        {
            if (a == null || b == null || a.Length != b.Length) return false;
            int diff = 0;
            for (int i = 0; i < a.Length; i++)
            {
                diff |= a[i] ^ b[i];
            }
            return diff == 0;
        }

        private byte[] EncryptAesCbc(byte[] plaintext, byte[] iv)
        {
            using (Aes aes = Aes.Create())
            {
                aes.Key = _aesKey;
                aes.IV = iv;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;
                using (ICryptoTransform transform = aes.CreateEncryptor())
                {
                    return transform.TransformFinalBlock(plaintext, 0, plaintext.Length);
                }
            }
        }

        private byte[] DecryptAesCbc(byte[] cipherText, byte[] iv)
        {
            using (Aes aes = Aes.Create())
            {
                aes.Key = _aesKey;
                aes.IV = iv;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;
                using (ICryptoTransform transform = aes.CreateDecryptor())
                {
                    return transform.TransformFinalBlock(cipherText, 0, cipherText.Length);
                }
            }
        }

        private byte[] ComputeMac(byte[] iv, byte[] cipherText)
        {
            using (HMACSHA256 hmac = new HMACSHA256(_hmacKey))
            {
                hmac.TransformBlock(iv, 0, IvSize, null, 0);
                hmac.TransformFinalBlock(cipherText, 0, cipherText.Length);
                return hmac.Hash;
            }
        }
    }
}
