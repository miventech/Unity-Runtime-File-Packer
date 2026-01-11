using System.IO;
using UnityEngine;
using Miventech.FilePacker;
using System.Text;

# if UNITY_EDITOR
using UnityEditor;
# endif

namespace Miventech.FilePacker.Examples
{
    public class FilePackerExample : MonoBehaviour
    {
        [Header("Package Configuration")]
        [SerializeField] private string _packageName = "ExamplePackage";
        
        [Header("Writing Settings")]
        [SerializeField] private string _sourceFile;
        [SerializeField] private string _nameInPackage = "my_asset.bin";
        [SerializeField] private bool _useCompression = true;
        [SerializeField] private bool _useEncryption = true;

        [Header("Reading Settings")]
        [SerializeField] private string _fileToRead = "my_asset.bin";
        [TextArea(3, 10)]
        [SerializeField] private string _outputLog;

        private string IndexPath => Path.Combine(Application.persistentDataPath, _packageName + ".ipk");

        public void PackSelectedFile()
        {
            if (string.IsNullOrEmpty(_sourceFile) || !File.Exists(_sourceFile))
            {
                Debug.LogError("[Example] Source file not found!");
                return;
            }

            Debug.Log($"--- Packing File: {_sourceFile} ---");

            using (var writer = new FilePackerWriter(IndexPath))
            {
                writer.AddFileToPackage(_sourceFile, _nameInPackage, compress: _useCompression, encrypt: _useEncryption);
                writer.Save();
            }

            Debug.Log("--- Packing Finished ---");
        }

        public void ReadSelectedFile()
        {
            Debug.Log($"--- Reading File: {_fileToRead} ---");

            using (var reader = new FilePackerReader(IndexPath))
            {
                if (reader.HasFile(_fileToRead))
                {
                    byte[] data = reader.ReadFile(_fileToRead);
                    _outputLog = Encoding.UTF8.GetString(data);
                    Debug.Log($"[Read Success] Data Length: {data.Length} bytes");
                }
                else
                {
                    _outputLog = "File not found in package.";
                    Debug.LogWarning("[Read Fail] File not found in index.");
                }
            }
        }

        public void ClearPackage()
        {
            using (var writer = new FilePackerWriter(IndexPath))
            {
                writer.RemoveExistingChunks();
                writer.ClearIndex();
            }
            _outputLog = "Package cleared.";
            Debug.Log("[Example] Package deleted successfully.");
        }
    }

    #if UNITY_EDITOR
    [CustomEditor(typeof(FilePackerExample))]
    public class FilePackerExampleEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            FilePackerExample example = (FilePackerExample)target;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Editor Actions", EditorStyles.boldLabel);

            // File Picker Row
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Browse Source File"))
            {
                string path = EditorUtility.OpenFilePanel("Select file to pack", "", "");
                if (!string.IsNullOrEmpty(path))
                {
                    // Update the private field via serialized object to ensure it saves
                    serializedObject.FindProperty("_sourceFile").stringValue = path;
                    serializedObject.ApplyModifiedProperties();
                }
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();

            if (GUILayout.Button("Pack File", GUILayout.Height(30)))
            {
                example.PackSelectedFile();
            }

            if (GUILayout.Button("Read File", GUILayout.Height(30)))
            {
                example.ReadSelectedFile();
            }

            GUI.color = new Color(1f, 0.4f, 0.4f);
            if (GUILayout.Button("Delete Package Files"))
            {
                if (EditorUtility.DisplayDialog("Delete Package", "Are you sure you want to delete the index and data chunks?", "Yes", "No"))
                {
                    example.ClearPackage();
                }
            }
            GUI.color = Color.white;
        }
    }
    #endif
}
