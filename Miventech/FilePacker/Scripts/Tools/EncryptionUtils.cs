using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Miventech.FilePacker.Tools
{
    // Keeping your files safe from prying eyes.
    public static class EncryptionUtils
    {
        private static byte[] GetKeyBytes(string key)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                return sha256.ComputeHash(Encoding.UTF8.GetBytes(key));
            }
        }

        // Lock it up using AES.
        public static byte[] Encrypt(byte[] data, string key)
        {
            if (data == null || data.Length == 0) return data;

            byte[] keyBytes = GetKeyBytes(key);
            using (Aes aes = Aes.Create())
            {
                aes.Key = keyBytes;
                // We're using a static IV based on the key to keep things simple.
                // This way we don't need to store a separate IV for every single file.
                byte[] iv = new byte[16];
                Array.Copy(keyBytes, iv, 16);
                aes.IV = iv;

                using (MemoryStream ms = new MemoryStream())
                {
                    using (CryptoStream cs = new CryptoStream(ms, aes.CreateEncryptor(), CryptoStreamMode.Write))
                    {
                        cs.Write(data, 0, data.Length);
                    }
                    return ms.ToArray();
                }
            }
        }

        // Unlock it. Same logic as above but in reverse.
        public static byte[] Decrypt(byte[] data, string key)
        {
            if (data == null || data.Length == 0) return data;

            byte[] keyBytes = GetKeyBytes(key);
            using (Aes aes = Aes.Create())
            {
                aes.Key = keyBytes;
                byte[] iv = new byte[16];
                Array.Copy(keyBytes, iv, 16);
                aes.IV = iv;

                using (MemoryStream ms = new MemoryStream())
                {
                    using (CryptoStream cs = new CryptoStream(ms, aes.CreateDecryptor(), CryptoStreamMode.Write))
                    {
                        cs.Write(data, 0, data.Length);
                    }
                    return ms.ToArray();
                }
            }
        }
    }
}
