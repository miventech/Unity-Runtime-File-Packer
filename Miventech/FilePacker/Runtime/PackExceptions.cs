using System;

namespace Miventech.FilePacker
{
    /// <summary>Excepción base de FilePacker.</summary>
    public class FilePackerException : Exception
    {
        public FilePackerException(string message) : base(message) { }
        public FilePackerException(string message, Exception inner) : base(message, inner) { }
    }

    /// <summary>La entrada solicitada no existe en el índice del paquete.</summary>
    public class PackFileNotFoundException : FilePackerException
    {
        public PackFileNotFoundException(string message) : base(message) { }
    }

    /// <summary>El contenido no coincide con su checksum/HMAC: paquete corrupto o manipulado.</summary>
    public class PackIntegrityException : FilePackerException
    {
        public PackIntegrityException(string message) : base(message) { }
    }

    /// <summary>El índice del paquete no se pudo cargar (corrupto o de otra versión).</summary>
    public class PackCorruptIndexException : FilePackerException
    {
        public PackCorruptIndexException(string message) : base(message) { }
    }
}
