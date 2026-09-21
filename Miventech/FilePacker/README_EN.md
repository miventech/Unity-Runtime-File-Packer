# FilePacker v2 (MVP1) — English

**FilePacker** is a Unity library to pack many files into binary containers (chunks) with hash-based indexing, compression (LZ4 / Deflate), encryption via **EasyCrypto**, CRC32 integrity checks, content listing, async reads and streaming.

[English README](README_EN.md) | [README en Español](README_ES.md)

## What's new in v2 (breaks the v5 format)

- **Fluent API**: `FilePack.Create(...).AddFile(...).AddData(...)` with auto-save on `Dispose`.
- **Real encryption**: master key protected by `EasyCrypto` (AES-256-CBC + HMAC-SHA256 + PBKDF2), random IV per entry, no hardcoded keys.
- **Integrity**: CRC32 checksum per entry, verified on read.
- **List contents**: `ListFiles()` returns all entry names.
- **Hierarchy**: `ListFiles("folder")` lists by folder and `GetHierarchy()` returns the folder/file tree (`PackNode`, with total size per folder).
- **Async**: `ReadFileAsync` / `ReadTextAsync` (Task-based).
- **Streaming**: `OpenRead()` decrypts/decompresses on-the-fly with incremental HMAC verification.
- **LZ4**: fast compression via **K4os.Compression.LZ4** (pure C#, MIT) — cross-platform (Windows/macOS/Linux/Android/iOS/WebGL, Mono and IL2CPP), managed DLL bundled at `Runtime/Plugins/Lz4/`.
- **asmdefs**: `Miventech.FilePacker.Runtime` + `Miventech.FilePacker.Editor`.

## Package structure

1. **Index (`.ipk`)**: binary v6 format (magic `MVPI`), entry table and master-key envelope when encrypted.
2. **Chunks (`_data_X.pkcam`)**: up to 4 GB per chunk (virtual global offsets).

## Install

1. Copy the folder into `Assets/`.
2. The library ships its own asmdef plus the managed LZ4 DLLs (K4os + Unsafe) at `Runtime/Plugins/Lz4/`.
3. Encryption uses **EasyCrypto** (`Miventech.Security`, the AES-Unity library); make sure it is in the project.

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

    string json = reader.ReadText("level1.json");
    byte[] asyncData = await reader.ReadFileAsync("textures/atlas.bin");
    Stream stream = reader.OpenRead("video.dat");
    bool ok = reader.VerifyAll();
}
```

## Editor window

`Tools → Miventech → File Packer`: pack, list, read, verify and remove entries without code.

Runtime example: `SceneTest/FilePackerRuntimeExample.cs` — add it to a GameObject and press Play.

## Technical notes

- **Naming**: names are normalized (lowercase, `\` → `/`) and hashed with FNV-1a 64-bit; the normalized name is stored in the index (that's why `ListFiles()` works).
- **Security (EasyCrypto envelope)**: the random 64-byte master key is protected with `EasyCrypto` (PBKDF2 runs once per package); every entry is AES-256-CBC with a random IV and an HMAC-SHA256 trailer (encrypt-then-MAC).
- **Dead space**: overwriting/removing entries leaves the old bytes in the chunk until a repack.
- **Streaming**: supports `None` and `Deflate`; LZ4 requires the full block (`ReadFile`).
- v6 is incompatible with v5: repack to migrate.

## Changelog

- **v2 (MVP1)** — Full rewrite: v6 index (names + CRC32), fluent API, async, streaming, EasyCrypto envelope, LZ4, asmdefs.
- **v1** — 4 GB chunks + hash index, Deflate, AES-CBC (fixed key, static IV).
