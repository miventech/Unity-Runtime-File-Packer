# FilePackerSystem

**FilePackerSystem** is a lightweight and efficient library for Unity designed to pack multiple files into binary containers (chunks). It uses a fast Hash-based indexing system to manage access to packed files with built-in compression and encryption support.

[English README](README_EN.md) | [README en Español](README_ES.md)

## Main Features

*   **Chunk-based Packing**: Supports large data volumes by splitting content into `.pkcam` files of up to 4GB (configurable).
*   **Integrated Compression**: Optional Deflate compression to save disk space.
*   **AES-256 Encryption**: Keep your assets secure with a customizable encryption key.
*   **Optimized Binary Index**: High-performance `.ipk` file for O(1) lookups (V5).
*   **Fast Search**: Implements the **FNV-1a 64-bit** algorithm with path normalization and lowercasing.
*   **Thread-Safe Reading (Basic)**: Allows simultaneous reads from multiple threads.
*   **Efficient Writing**: Caches streams during massive batch operations.

## File Structure

1.  **Index File (`.ipk`)**: Stores the allocation table and metadata (Version 5).
2.  **Data Chunks (`_data_X.pkcam`)**: Binary containers for the packed content.

## Quick Start

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

**Key Methods:**
*   `AddFileToPackage(..., bool compress = false, bool encrypt = false)`: Main method to add content.
*   `RemoveExistingChunks()`: Physically deletes data files.
*   `ClearIndex()`: Resets the entire index.

### Reading Files (FilePackerReader)

The reader handles decompression and decryption automatically based on the index metadata.

**Basic Example:**

```csharp
var reader = new FilePackerReader(indexAPath);

if (reader.HasFile("image.png"))
{
    byte[] fileData = reader.ReadFile("image.png");
    // Works the same if the file was compressed or encrypted.
}
```

## Technical Details

### Security
Change the `EncryptionKey` in `SettingFilePacker.cs` before building your project to ensure asset privacy.

### Hashing and Paths
Windows uses `\` but everyone else loves `/`? No problem. The system normalizes all paths to `/` and converts them to lowercase before hashing, so `Folder\File.txt` and `folder/file.txt` point to the same data.

### Limitations
*   **Garbage Space**: Deleting a file only removes its index entry. The bytes remain in the chunk as "dead space" until a full repack (clear and rebuild) is performed.
