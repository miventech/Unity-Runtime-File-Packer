using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using Miventech.FilePacker;
using Miventech.FilePacker.Tools;

namespace Miventech.FilePacker.Editor
{
    /// <summary>
    /// Ventana editor para probar FilePacker v2 sin escribir código:
    /// Tools → Miventech → File Packer.
    /// </summary>
    public sealed class FilePackerWindow : EditorWindow
    {
        private string _indexPath = "";
        private string _sourceFile = "";
        private string _entryName = "";
        private string _password = "";
        private bool _encrypt;
        private int _codecIndex;
        private static readonly string[] CodecLabels = { "None", "Deflate", "LZ4" };
        private PackNode _lastHierarchy;
        private string _lastResult = "";

        [MenuItem("Tools/Miventech/File Packer")]
        public static void Open()
        {
            FilePackerWindow window = GetWindow<FilePackerWindow>();
            window.titleContent = new GUIContent("File Packer");
            window.minSize = new Vector2(420, 420);
            window.Show();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("FilePacker v2 (MVP1)", EditorStyles.boldLabel);

            EditorGUILayout.LabelField("Índice del paquete (.ipk)");
            DrawPathField(ref _indexPath);

            EditorGUILayout.Space();
            _codecIndex = EditorGUILayout.Popup("Codec", _codecIndex, CodecLabels);
            _encrypt = EditorGUILayout.Toggle("Cifrar", _encrypt);
            _password = EditorGUILayout.PasswordField("Password", _password);

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Añadir entrada", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Archivo de origen");
            DrawPathField(ref _sourceFile);
            _entryName = EditorGUILayout.TextField("Nombre en el paquete", _entryName);

            EditorGUILayout.Space(4);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Pack (añadir y guardar)")) PackEntryButton();
            if (GUILayout.Button("Eliminar entrada")) RemoveEntryButton();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Lectura / verificación", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Listar contenido")) ListButton();
            if (GUILayout.Button("Leer entrada")) ReadButton();
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Verificar integridad")) VerifyButton();
            if (GUILayout.Button("Leer como stream")) ReadAsStreamButton();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Resultado", EditorStyles.boldLabel);
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
            EditorGUILayout.HelpBox(string.IsNullOrEmpty(_lastResult) ? "Sin resultados todavía." : _lastResult, MessageType.Info);
            if (_lastHierarchy != null)
            {
                foreach (PackNode child in _lastHierarchy.Children)
                {
                    DrawNode(child);
                }
            }
            EditorGUILayout.EndScrollView();
        }

        private Vector2 _scrollPosition;

        private static void DrawPathField(ref string path)
        {
            EditorGUILayout.BeginHorizontal();
            path = EditorGUILayout.TextField(path);
            if (GUILayout.Button("...", GUILayout.Width(28)))
            {
                string picked = EditorUtility.OpenFilePanel("Seleccionar archivo", "", "");
                if (!string.IsNullOrEmpty(picked)) path = picked;
            }
            EditorGUILayout.EndHorizontal();
        }

        private EntryCodecMode SelectedCodec()
        {
            return (EntryCodecMode)_codecIndex;
        }

        private void PackEntryButton()
        {
            try
            {
                if (string.IsNullOrEmpty(_indexPath) || string.IsNullOrEmpty(_sourceFile)) throw new ArgumentException("Índice y archivo de origen son obligatorios.");
                string name = string.IsNullOrEmpty(_entryName) ? Path.GetFileName(_sourceFile) : _entryName;
                var options = new PackOptions { Codec = SelectedCodec(), Encrypt = _encrypt, Password = _password ?? "" };
                using (FilePack pack = FilePack.Create(_indexPath, options))
                {
                    pack.AddFile(_sourceFile, name);
                    _lastResult = "Añadido '" + name + "' (" + pack.EntryCount + " entradas). Guardado en:\n" + _indexPath;
                }
                AssetDatabase.Refresh();
            }
            catch (Exception e)
            {
                EditorUtility.DisplayDialog("FilePacker", e.Message, "OK");
            }
        }

        private void RemoveEntryButton()
        {
            try
            {
                if (string.IsNullOrEmpty(_indexPath)) throw new ArgumentException("Índice obligatorio.");
                var options = new PackOptions { Password = _password ?? "" };
                using (FilePack pack = FilePack.Create(_indexPath, options))
                {
                    bool removed = pack.TryRemove(_entryName);
                    _lastResult = removed ? "Entrada eliminada: " + _entryName : "No existía: " + _entryName;
                }
            }
            catch (Exception e)
            {
                EditorUtility.DisplayDialog("FilePacker", e.Message, "OK");
            }
        }

        private void ListButton()
        {
            try
            {
                using (PackReader reader = new PackReader(_indexPath, _encrypt ? _password : null))
                {
                    _lastHierarchy = reader.GetHierarchy();
                    _lastResult = reader.EntryCount + " entradas en " + _lastHierarchy.Children.Count + " elemento(s) de raíz.";
                }
            }
            catch (Exception e)
            {
                _lastHierarchy = null;
                EditorUtility.DisplayDialog("FilePacker", e.Message, "OK");
            }
        }

        private void ReadButton()
        {
            try
            {
                using (PackReader reader = new PackReader(_indexPath, _encrypt ? _password : null))
                {
                    byte[] data;
                    if (!reader.TryReadFile(_entryName, out data))
                    {
                        _lastResult = "No existe: " + _entryName;
                        return;
                    }
                    _lastResult = "Leído '" + _entryName + "': " + data.Length + " bytes, CRC32 OK.\nInicio: " + Preview(data);
                    string destination = EditorUtility.SaveFilePanel("Guardar entrada leída", "", Path.GetFileName(_entryName), "");
                    if (!string.IsNullOrEmpty(destination)) File.WriteAllBytes(destination, data);
                }
            }
            catch (Exception e)
            {
                EditorUtility.DisplayDialog("FilePacker", e.Message, "OK");
            }
        }

        private void ReadAsStreamButton()
        {
            try
            {
                using (PackReader reader = new PackReader(_indexPath, _encrypt ? _password : null))
                using (Stream stream = reader.OpenRead(_entryName))
                {
                    var buffer = new MemoryStream();
                    stream.CopyTo(buffer);
                    byte[] data = buffer.ToArray();
                    _lastResult = "Stream leído '" + _entryName + "': " + data.Length + " bytes (HMAC/deflate verificados en streaming).\nInicio: " + Preview(data);
                }
            }
            catch (Exception e)
            {
                EditorUtility.DisplayDialog("FilePacker", e.Message, "OK");
            }
        }

        private void VerifyButton()
        {
            try
            {
                using (PackReader reader = new PackReader(_indexPath, _encrypt ? _password : null))
                {
                    bool ok = reader.VerifyAll();
                    _lastResult = ok ? "Integridad OK: " + reader.EntryCount + " entradas verificadas (CRC32)." : "Integridad FALLIDA (ver consola).";
                }
            }
            catch (Exception e)
            {
                EditorUtility.DisplayDialog("FilePacker", e.Message, "OK");
            }
        }

        private static string Preview(byte[] data)
        {
            int take = Mathf.Min(24, data.Length);
            return Encoding.UTF8.GetString(data, 0, take).Replace("\n", " ").Replace("\r", " ");
        }

        private static void DrawNode(PackNode node)
        {
            if (node.IsFolder)
            {
                EditorGUILayout.LabelField(node.Name + "/", EditorStyles.boldLabel);
                EditorGUI.indentLevel++;
                for (int i = 0; i < node.Children.Count; i++)
                {
                    DrawNode(node.Children[i]);
                }
                EditorGUI.indentLevel--;
            }
            else
            {
                EditorGUILayout.LabelField(node.Name, FormatSize(node.Entry.UncompressedLength));
            }
        }

        private static string FormatSize(long bytes)
        {
            if (bytes < 1024) return bytes + " bytes";
            if (bytes < 1024 * 1024) return (bytes / 1024.0).ToString("F1") + " KB";
            return (bytes / (1024.0 * 1024.0)).ToString("F2") + " MB";
        }
    }
}
