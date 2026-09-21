# FilePacker v2 (MVP1)

**FilePacker** es una librería de Unity para empaquetar múltiples archivos en contenedores binarios (chunks) con indexado por hash, compresión (LZ4 / Deflate), cifrado con **EasyCrypto** (AES-Unity), verificación de integridad CRC32, listado de contenido, API async y streaming.

[English README](README_EN.md) | [README en Español](README_ES.md)

## Novedades de la v2 (rompe compatibilidad con el formato v5)

- **API fluida**: `FilePack.Create(...).AddFile(...).AddData(...)` con auto-guardado en `Dispose`.
- **Cifrado serio**: clave maestra protegida con `EasyCrypto` (AES-256-CBC + HMAC-SHA256 + PBKDF2), IV aleatorio por entrada. Nada de claves hardcodeadas.
- **Integridad**: CRC32 por entrada, verificado en cada lectura.
- **Listado de contenido**: `ListFiles()` devuelve los nombres de las entradas.
- **Jerarquía**: `ListFiles("datos")` lista por carpeta y `GetHierarchy()` devuelve el árbol de carpetas/archivos (`PackNode`, con tamaño total por carpeta).
- **Async**: `ReadFileAsync` / `ReadTextAsync` (Task-based).
- **Streaming**: `OpenRead()` descifra y descomprime on-the-fly con verificación HMAC incremental.
- **LZ4**: compresión rápida vía **K4os.Compression.LZ4** (C# puro, MIT) — multiplataforma (Windows/macOS/Linux/Android/iOS/WebGL, Mono e IL2CPP), DLL incluida en `Runtime/Plugins/Lz4/`.
- **asmdef**: `Miventech.FilePacker.Runtime` + `Miventech.FilePacker.Editor`.

## Estructura del paquete

1. **Índice (`.ipk`)**: formato binario v6 (magic `MVPI`), tabla de entradas y sobre de clave maestra si hay cifrado.
2. **Chunks (`_data_X.pkcam`)**: datos hasta 4 GB por chunk (offset global virtual).

## Instalación

1. Copia la carpeta en `Assets/`.
2. Listo: la librería trae asmdef propio y las DLL managed de LZ4 (K4os + Unsafe) en `Runtime/Plugins/Lz4/`.
3. El cifrado usa **EasyCrypto** (`Miventech.Security`, librería AES-Unity); asegúrate de que está en el proyecto.

## Quick Start

```csharp
using Miventech.FilePacker;

// --- Escribir ---
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

// --- Lectura ---
using (PackReader reader = new PackReader(indexPath, "mi-password"))
{
    string[] archivos = reader.ListFiles();

    byte[] data;
    if (reader.TryReadFile("nivel1.json", out data)) { /* false solo si NO existe */ }

    string json = reader.ReadText("nivel1.json");

    byte[] asyncData = await reader.ReadFileAsync("texturas/atlas.bin");

    Stream stream = reader.OpenRead("video.dat"); // descifrado on-the-fly

    bool ok = reader.VerifyAll(); // CRC32 de todas las entradas
}
```

## Ventana de editor

`Tools → Miventech → File Packer`: empaqueta, lista, lee, verifica y elimina entradas sin escribir código.

Ejemplo runtime: `SceneTest/FilePackerRuntimeExample.cs` — añádelo a un GameObject y pulsa Play.

## Detalles técnicos

### Seguridad (sobre EasyCrypto)
La clave maestra (64 bytes aleatorios: 32 AES + 32 HMAC) se protege con `EasyCrypto.Encrypt(clave, password)` (PBKDF2 con salt aleatorio). El KDF caro corre **una vez por paquete**; cada entrada se cifra con AES-256-CBC + IV aleatorio + HMAC-SHA256 (encrypt-then-MAC). Si la password es incorrecta, falla al abrir el paquete con `FilePackerException`.

### Nombres y hash
Todo nombre se normaliza (minúsculas, `\` → `/`) con FNV-1a 64-bit. El nombre normalizado se guarda en el índice, por eso `ListFiles()` funciona.

### Limitaciones
- **Espacio muerto**: sobrescribir/eliminar entradas no compacta los chunks (repack = crear paquete nuevo).
- **Streaming**: `OpenRead()` soporta `None` y `Deflate`; LZ4 requiere el bloque completo (`ReadFile`).
- Formato v6 incompatible con paquetes v5: repaqueta.

## Changelog

- **v2 (MVP1)** — Reescritura completa: índice v6 (nombres + CRC32), API fluida, async, streaming, sobre EasyCrypto, LZ4, asmdefs.
- **v1** — Chunks de 4 GB + índice por hash, Deflate, AES-CBC (clave fija, IV estático).
