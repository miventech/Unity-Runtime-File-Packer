using System;
using System.Collections.Generic;
using Miventech.FilePacker.Tools;

namespace Miventech.FilePacker
{
    /// <summary>
    /// Nodo del árbol de jerarquía del contenido de un paquete.
    /// Las carpetas derivan de la separación por '/' en los nombres normalizados.
    /// </summary>
    public sealed class PackNode
    {
        public string Name;          // "nivel1.json" o "datos"
        public string Path;          // ruta completa "datos/nivel1.json"
        public bool IsFolder;        // true para carpetas intermedias
        public PackEntry Entry;      // válido solo en archivos (!IsFolder)
        public List<PackNode> Children = new List<PackNode>();

        public long TotalSize
        {
            get
            {
                if (!IsFolder) return Entry.UncompressedLength;
                long total = 0;
                for (int i = 0; i < Children.Count; i++)
                {
                    total += Children[i].TotalSize;
                }
                return total;
            }
        }
    }

    /// <summary>
    /// Construye la jerarquía de carpetas/archivos a partir de las entradas del índice.
    /// No requiere cambios en el formato: los nombres ya usan '/' como separador.
    /// </summary>
    public static class PackHierarchy
    {
        public static PackNode Build(IEnumerable<PackEntry> entries)
        {
            if (entries == null) throw new ArgumentNullException("entries");
            var root = new PackNode { Name = "", Path = "", IsFolder = true };
            foreach (PackEntry entry in entries)
            {
                Insert(root, entry);
            }
            Sort(root);
            return root;
        }

        private static void Insert(PackNode root, PackEntry entry)
        {
            string normalized = HashUtils.NormalizeName(entry.Name);
            string[] parts = normalized.Split('/');
            PackNode current = root;
            string path = "";

            for (int i = 0; i < parts.Length - 1; i++)
            {
                path = (path.Length == 0) ? parts[i] : path + "/" + parts[i];
                current = GetOrCreateFolder(current, parts[i], path);
            }

            var file = new PackNode
            {
                Name = parts[parts.Length - 1],
                Path = normalized,
                IsFolder = false,
                Entry = entry,
            };
            current.Children.Add(file);
        }

        private static PackNode GetOrCreateFolder(PackNode parent, string name, string path)
        {
            for (int i = 0; i < parent.Children.Count; i++)
            {
                PackNode child = parent.Children[i];
                if (child.IsFolder && string.Equals(child.Name, name, StringComparison.Ordinal)) return child;
            }
            var folder = new PackNode { Name = name, Path = path, IsFolder = true };
            parent.Children.Add(folder);
            return folder;
        }

        private static void Sort(PackNode node)
        {
            node.Children.Sort(Compare);
            for (int i = 0; i < node.Children.Count; i++)
            {
                if (node.Children[i].IsFolder) Sort(node.Children[i]);
            }
        }

        private static int Compare(PackNode a, PackNode b)
        {
            if (a.IsFolder != b.IsFolder) return a.IsFolder ? -1 : 1;
            return string.Compare(a.Name, b.Name, StringComparison.Ordinal);
        }
    }
}
