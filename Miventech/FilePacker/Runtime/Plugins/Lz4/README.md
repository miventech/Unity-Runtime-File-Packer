# Plugins: LZ4 (K4os.Compression.LZ4)

DLLs managed (C# puro) incluidas con FilePacker para el codec LZ4 de las entradas.

## Origen y licencia

| Archivo | Origen | Licencia |
|---|---|---|
| `K4os.Compression.LZ4.dll` | NuGet `K4os.Compression.LZ4` 1.3.8, build `netstandard2.1` | MIT (© Milosz Krajewski) |
| `System.Runtime.CompilerServices.Unsafe.dll` | NuGet `System.Runtime.CompilerServices.Unsafe` 6.0.0, build `netstandard2.0` | MIT (© .NET Foundation) |

- Ambas son **100% managed** (sin binarios nativos): funcionan en Windows, macOS, Linux, Android, iOS y WebGL, con Mono e IL2CPP.
- `System.Memory` y `System.Buffers` NO se incluyen porque Unity las provee vía el perfil .NET Standard 2.1 (shims del editor).
- API usada por `EntryCodec`: `LZ4Pickler.Pickle(byte[])` / `LZ4Pickler.Unpickle(byte[])` (formato auto-descriptivo, guarda la longitud original).
- Fuente del paquete: https://www.nuget.org/packages/K4os.Compression.LZ4
