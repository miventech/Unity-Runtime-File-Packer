using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Miventech.FilePacker;

namespace Miventech.FilePacker.Examples
{
    /// <summary>
    /// Ejemplo runtime de FilePacker v2. Añade este componente a cualquier
    /// GameObject en una escena y pulsa Play: escribe un paquete con cifrado
    /// en Application.persistentDataPath, lo lee (sync, async y streaming) y
    /// verifica la integridad. La escena .unity la creas tú desde el editor.
    /// </summary>
    public class FilePackerRuntimeExample : MonoBehaviour
    {
        private const string Password = "demo-password-123";

        private async void Start()
        {
            string directory = Path.Combine(Application.persistentDataPath, "FilePackerDemo");
            string indexPath = Path.Combine(directory, "demo.ipk");

            WriteDemo(indexPath);
            await ReadDemoAsync(indexPath);
        }

        private static void WriteDemo(string indexPath)
        {
            var options = new PackOptions
            {
                Codec = EntryCodecMode.Deflate,
                Encrypt = true,
                Password = Password,
            };

            using (FilePack pack = FilePack.Create(indexPath, options))
            {
                pack.AddData(Encoding.UTF8.GetBytes("{\"nivel\":1,\"enemigos\":8}"), "datos/nivel1.json");
                pack.AddData(Encoding.UTF8.GetBytes("{\"nivel\":2,\"enemigos\":14}"), "datos/nivel2.json");

                byte[] blob = new byte[64 * 1024];
                for (int i = 0; i < blob.Length; i++)
                {
                    blob[i] = (byte)(i % 251);
                }
                pack.AddData(blob, "datos/blob.bin");

                Debug.Log("[FilePackerDemo] Paquete escrito con " + pack.EntryCount + " entradas: " + indexPath);
            } // Dispose → Save automático
        }

        private static async Task<string> ReadDemoAsync(string indexPath)
        {
            if (!File.Exists(indexPath))
            {
                Debug.Log("[FilePackerDemo] No existe el paquete demo; escribe uno primero (WriteDemo o la ventana Tools → Miventech → File Packer).");
                return null;
            }

            using (PackReader reader = new PackReader(indexPath, Password))
            {
                string listing = string.Join(", ", reader.ListFiles());
                Debug.Log("[FilePackerDemo] Contenido: " + listing);

                byte[] data;
                if (reader.TryReadFile("datos/nivel1.json", out data))
                {
                    Debug.Log("[FilePackerDemo] nivel1.json = " + Encoding.UTF8.GetString(data));
                }

                byte[] asyncData = await reader.ReadFileAsync("datos/blob.bin");
                Debug.Log("[FilePackerDemo] blob.bin (async) = " + asyncData.Length + " bytes");

                using (Stream stream = reader.OpenRead("datos/nivel2.json"))
                {
                    using (var memory = new MemoryStream())
                    {
                        stream.CopyTo(memory);
                        Debug.Log("[FilePackerDemo] nivel2.json (stream) = " + Encoding.UTF8.GetString(memory.ToArray()));
                    }
                }

                bool integrity = reader.VerifyAll();
                Debug.Log("[FilePackerDemo] Integridad CRC32: " + (integrity ? "OK" : "FALLIDA"));
                return listing;
            }
        }
    }
}
