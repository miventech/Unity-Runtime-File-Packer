# FilePackerSystem

**FilePackerSystem** es una librería ligera y eficiente para Unity diseñada para empaquetar múltiples archivos en contenedores binarios (chunks). Utiliza un sistema de indexado rápido basado en Hash para gestionar el acceso a los archivos empaquetados con soporte para compresión y encriptación.

[English README](README_EN.md) | [README en Español](README_ES.md)

## Características Principales

*   **Empaquetado en Chunks**: Soporta grandes volúmenes de datos dividiendo el contenido en archivos `.pkcam` de hasta 4GB (configurable).
*   **Compresión Integrada**: Compresión Deflate opcional para ahorrar espacio en disco.
*   **Encriptación AES-256**: Mantén tus assets seguros con una clave de encriptación personalizable.
*   **Índice Binario Optimizado**: Archivo `.ipk` de alto rendimiento para búsquedas O(1) (V5).
*   **Búsqueda Rápida**: Implementa el algoritmo **FNV-1a 64-bit** con normalización de rutas y minúsculas.
*   **Lectura Thread-Safe (Básica)**: Permite lecturas simultáneas desde múltiples hilos.
*   **Escritura Eficiente**: Mantiene los streams en caché durante operaciones masivas.

## Estructura de Archivos

1.  **Archivo de Índice (`.ipk`)**: Almacena la tabla de asignación y metadatos (Versión 5).
2.  **Chunks de Datos (`_data_X.pkcam`)**: Contenedores binarios para el contenido empaquetado.

## Guía de Uso

### Namespace
```csharp
using Miventech.FilePacker;
```

### Escritura de Archivos (FilePackerWriter)

**Ejemplo Básico:**

```csharp
string indexAPath = Path.Combine(Application.persistentDataPath, "MyPackage.ipk");

using (var writer = new FilePackerWriter(indexAPath))
{
    // Agregar archivo con compresión y encriptación
    writer.AddFileToPackage("C:/Assets/textura.png", "imagen.png", compress: true, encrypt: true);

    // Agregar bytes crudos
    byte[] data = System.Text.Encoding.UTF8.GetBytes("Datos Secretos");
    writer.AddFileToPackage(data, "secreto.txt", compress: true, encrypt: true);
    
    writer.Save();
}
```

**Métodos Importantes:**
*   `AddFileToPackage(..., bool compress = false, bool encrypt = false)`: El método principal para añadir contenido.
*   `RemoveExistingChunks()`: Borra los archivos de datos físicos.
*   `ClearIndex()`: Resetea todo el índice.

### Lectura de Archivos (FilePackerReader)

El lector maneja automáticamente la descompresión y desencriptación basándose en el índice.

**Ejemplo Básico:**

```csharp
var reader = new FilePackerReader(indexAPath);

if (reader.HasFile("imagen.png"))
{
    byte[] fileData = reader.ReadFile("imagen.png");
    // Funciona igual si el archivo estaba comprimido o encriptado.
}
```

## Detalles Técnicos

### Seguridad
Cambia la `EncryptionKey` en `SettingFilePacker.cs` antes de compilar tu proyecto para asegurar la privacidad de tus assets.

### Hashing y Rutas
¿Windows usa `\` pero los demás prefieren `/`? No hay problema. El sistema normaliza todas las rutas a `/` y las convierte a minúsculas antes de generar el hash, por lo que `Carpeta\Archivo.txt` y `carpeta/archivo.txt` apuntan a los mismos datos.

### Limitaciones
*   **Espacio Basura**: Borrar un archivo solo elimina su entrada en el índice. Los bytes permanecen en el chunk como "espacio muerto" hasta que realices un re-empaquetado completo (limpiar y reconstruir).
