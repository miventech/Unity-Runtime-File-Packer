# FilePackerSystem

**FilePackerSystem** is a lightweight and efficient library for Unity designed to pack multiple files into binary containers (chunks). It uses a fast Hash-based indexing system to manage access to the packed files with support for compression and encryption.

## Main Features

*   **Chunk-based Packing**: Supports large data volumes by splitting content into `.pkcam` files of up to 4GB (configurable).
*   **Built-in Compression**: Optional Deflate compression to save disk space.
*   **AES-256 Encryption**: Keep your assets safe with a customizable encryption key.
*   **Optimized Binary Index**: High-performance `.ipk` file for O(1) file lookups.
*   **Fast Search**: Implements a case-insensitive, path-normalized **FNV-1a 64-bit** hash algorithm.
*   **Basic Thread-Safe Reading**: Simultaneous reads from multiple threads via shared `FileStream` with locks.
*   **Efficient Writing**: Keeps streams cached during massive operations to maximize throughput.

## File Structure

1.  **Index File (`.ipk`)**: Stores the allocation table (V5 supports compression/encryption flags).
2.  **Data Chunks (`_data_X.pkcam`)**: Raw binary containers for your packed content.

## Usage Guide

### Namespace
```csharp
using Miventech.FilePacker;
```

### Writing Files (FilePackerWriter)

**Basic Example:**

```csharp
string indexAPath = Path.Combine(Application.persistentDataPath, "MyPackage.ipk");

using (var writer = new FilePackerWriter(indexAPath))
{
    // Add file with compression and encryption
    writer.AddFileToPackage("C:/Assets/texture.png", "image.png", compress: true, encrypt: true);

    // Add raw bytes
    byte[] data = System.Text.Encoding.UTF8.GetBytes("Secret Data");
    writer.AddFileToPackage(data, "secret.txt", compress: true, encrypt: true);
    
    writer.Save();
}
```

**Important Methods:**
*   `AddFileToPackage(..., bool compress = false, bool encrypt = false)`: The power horse for adding content.
*   `RemoveExistingChunks()`: Clear physical data files.
*   `ClearIndex()`: Reset the entire index.

### Reading Files (FilePackerReader)

The reader automatically handles decompression and decryption based on index flags.

**Basic Example:**

```csharp
var reader = new FilePackerReader(indexAPath);

if (reader.HasFile("image.png"))
{
    byte[] fileData = reader.ReadFile("image.png");
    // Works regardless of whether the file was compressed or encrypted!
}
```

## Technical Details

### Security
Change the `EncryptionKey` in `SettingFilePacker.cs` before building your project to ensure your assets stay private.

### Hashing & Paths
Wait, Windows uses `\` but everyone else likes `/`? No problem. The system normalizes all paths to `/` and converts them to lowercase before hashing, so `Folder\File.txt` and `folder/file.txt` point to the same data.

### Limitations
*   **Garbage Space**: Deleting a file only removes its index entry. The bytes stay in the chunk as "dead space" until you perform a full repack (manual clear and rebuild).
