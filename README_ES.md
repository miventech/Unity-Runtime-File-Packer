# FilePacker v2 (MVP1)

**FilePacker** es una librería de Unity para empaquetar muchos archivos en contenedores binarios (chunks) con indexado por hash, compresión (LZ4 / Deflate), cifrado con **EasyCrypto**, integridad CRC32, listado de contenido, async y streaming.

[English README](README_EN.md) | [README en Español](README_ES.md)

> Documentación completa (v2): [Miventech/FilePacker/README_ES.md](Miventech/FilePacker/README_ES.md)

## Características principales

- **API fluida**: `FilePack.Create(...).AddFile(...).AddData(...)` con auto-guardado en `Dispose`.
- **Cifrado**: clave maestra protegida con `EasyCrypto` (AES-256-CBC + HMAC-SHA256 + PBKDF2), IV aleatorio por entrada, nada de claves hardcodeadas.
- **Integridad**: checksum CRC32 por entrada, verificado al leer.
- **Listado / Async / Streaming**: `ListFiles()`, `ListFiles("carpeta")`, `GetHierarchy()` (árbol de carpetas/archivos), `ReadFileAsync`, `OpenRead()` (descifrado/descompresión on-the-fly).
- **LZ4**: codec rápido vía **K4os.Compression.LZ4** (C# puro, MIT, multiplataforma: Windows/macOS/Linux/Android/iOS/WebGL, Mono e IL2CPP) — DLLs managed incluidas en `Runtime/Plugins/Lz4/`.
- **asmdef**: `Miventech.FilePacker.Runtime` + `Miventech.FilePacker.Editor`.

## Estructura del paquete

1. **Índice (`.ipk`)**: formato binario v6 (magic `MVPI`) con nombres de entradas + checksums CRC32.
2. **Chunks (`_data_X.pkcam`)**: hasta 4 GB por chunk (offsets globales virtuales).

## Uso rápido

```csharp
using Miventech.FilePacker;

string indexPath = Path.Combine(Application.persistentDataPath, "game.ipk");
using (FilePack pack = FilePack.Create(indexPath, new PackOptions
{
    Codec = EntryCodecMode.Lz4,
    Encrypt = true,
    Password = "mi-password",
}))
{
    pack.AddFile(rutaEnDisco, "nivel1.json")
        .AddData(bytesEnMemoria, "texturas/atlas.png");
} // Dispose → Save() automático

using (PackReader reader = new PackReader(indexPath, "mi-password"))
{
    string[] archivos = reader.ListFiles();
    byte[] data;
    if (reader.TryReadFile("nivel1.json", out data)) { /* false solo si NO existe */ }
    byte[] asyncData = await reader.ReadFileAsync("texturas/atlas.bin");
    Stream stream = reader.OpenRead("video.dat");
    bool ok = reader.VerifyAll();
}
```

## Ventana de editor

`Tools → Miventech → File Packer`: empaqueta, lista, lee, verifica y elimina entradas sin escribir código.

## Notas

- **Espacio muerto**: sobrescribir/eliminar entradas deja los bytes viejos en el chunk hasta un repack.
- **Streaming**: soporta `None` y `Deflate`; LZ4 requiere el bloque completo (`ReadFile`).
- **v6 incompatible con v5**: repaqueta para migrar.
