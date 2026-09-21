# FilePacker v2 (MVP1)

**FilePacker** is a Unity library to pack many files into binary containers (chunks) with hash-based indexing, compression (LZ4 / Deflate), encryption via **EasyCrypto**, CRC32 integrity checks, content listing, async reads and streaming.

[English README](README_EN.md) | [README en Español](README_ES.md)

> Full documentation (v2): [Miventech/FilePacker/README_EN.md](Miventech/FilePacker/README_EN.md)

## Main Features

- **Fluent API**: `FilePack.Create(...).AddFile(...).AddData(...)` with auto-save on `Dispose`.
- **Encryption**: master key protected by `EasyCrypto` (AES-256-CBC + HMAC-SHA256 + PBKDF2), random IV per entry, no hardcoded keys.
- **Integrity**: CRC32 checksum per entry, verified on read.
- **Listing / Async / Streaming**: `ListFiles()`, `ListFiles("folder")`, `GetHierarchy()` (folder/file tree), `ReadFileAsync`, `OpenRead()` (on-the-fly decrypt/decompress).
- **LZ4**: fast codec via **K4os.Compression.LZ4** (pure C#, MIT, cross-platform: Windows/macOS/Linux/Android/iOS/WebGL, Mono and IL2CPP) — managed DLLs bundled at `Runtime/Plugins/Lz4/`.
- **asmdefs**: `Miventech.FilePacker.Runtime` + `Miventech.FilePacker.Editor`.

## Package structure

1. **Index (`.ipk`)**: binary v6 format (magic `MVPI`) with entry names + CRC32 checksums.
2. **Chunks (`_data_X.pkcam`)**: up to 4 GB per chunk (virtual global offsets).

## Quick Start

```csharp
using Miventech.FilePacker;

string indexPath = Path.Combine(Application.persistentDataPath, "game.ipk");
using (FilePack pack = FilePack.Create(indexPath, new PackOptions
{
    Codec = EntryCodecMode.Lz4,
    Encrypt = true,
    Password = "my-password",
}))
{
    pack.AddFile(pathOnDisk, "level1.json")
        .AddData(bytesInMemory, "textures/atlas.png");
} // Dispose → automatic Save()

using (PackReader reader = new PackReader(indexPath, "my-password"))
{
    string[] files = reader.ListFiles();
    byte[] data;
    if (reader.TryReadFile("level1.json", out data)) { /* false only if missing */ }
    byte[] asyncData = await reader.ReadFileAsync("textures/atlas.bin");
    Stream stream = reader.OpenRead("video.dat");
    bool ok = reader.VerifyAll();
}
```

## Editor window

`Tools → Miventech → File Packer`: pack, list, read, verify and remove entries without code.

## Notes

- **Dead space**: overwriting/removing entries leaves old bytes in the chunk until a repack.
- **Streaming**: supports `None` and `Deflate`; LZ4 requires the full block (`ReadFile`).
- **v6 is incompatible with v5**: repack to migrate.
