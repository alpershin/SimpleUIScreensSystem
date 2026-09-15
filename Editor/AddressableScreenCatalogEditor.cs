using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using SimpleUIScreensSystem.AddressableUI;
using UnityEditor;
using UnityEngine;

namespace SimpleUIScreensSystem.Editor
{
    [CustomEditor(typeof(AddressableScreenCatalog))]
    internal sealed class AddressableScreenCatalogEditor : UnityEditor.Editor
    {
        private bool _showScreenIds;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            DrawStableIds();
            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(
                "Set a unique Code Name such as Settings for each screen. Generated keys use the stable ID; changing a code name or prefab keeps that ID. Save the generated file in your game's Assets folder.",
                MessageType.Info);
            if (GUILayout.Button("Generate Screen Keys...")) GenerateKeys();
        }

        private void DrawStableIds()
        {
            var catalog = (AddressableScreenCatalog)target;
            if (catalog.Screens == null) return;
            var ids = new HashSet<ScreenId>();
            var hasDuplicates = false;
            _showScreenIds = EditorGUILayout.Foldout(_showScreenIds, "Stable screen IDs");
            foreach (var screen in catalog.Screens)
            {
                if (screen == null) continue;
                if (screen.Id.IsValid && !ids.Add(screen.Id)) hasDuplicates = true;
                if (!_showScreenIds) continue;
                using (new EditorGUI.DisabledScope(true))
                    EditorGUILayout.TextField(string.IsNullOrEmpty(screen.CodeName) ? "Unnamed screen" : screen.CodeName,
                        screen.Id.Value);
            }
            if (!hasDuplicates) return;

            EditorGUILayout.HelpBox(
                "Copied entries share stable IDs. Assign new IDs to the copies, then regenerate keys. The first entry with each ID keeps its identity.",
                MessageType.Warning);
            if (!GUILayout.Button("Assign New IDs to Duplicate Entries")) return;

            ids.Clear();
            Undo.RecordObject(catalog, "Assign new IDs to duplicate screens");
            serializedObject.Update();
            var screens = serializedObject.FindProperty("_screens");
            for (var i = 0; i < screens.arraySize; i++)
            {
                var id = screens.GetArrayElementAtIndex(i).FindPropertyRelative("_id");
                if (!string.IsNullOrWhiteSpace(id.stringValue) && !ids.Add(new ScreenId(id.stringValue)))
                    id.stringValue = Guid.NewGuid().ToString("N");
            }
            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(catalog);
        }

        private void GenerateKeys()
        {
            var catalog = (AddressableScreenCatalog)target;
            try
            {
                var catalogPath = AssetDatabase.GetAssetPath(catalog);
                var catalogGuid = AssetDatabase.AssetPathToGUID(catalogPath);
                Undo.RecordObject(catalog, "Assign stable screen IDs");
                catalog.EnsureScreenIds();
                EditorUtility.SetDirty(catalog);
                AssetDatabase.SaveAssetIfDirty(catalog);

                if (catalog.Screens == null) throw new InvalidOperationException("Screen catalog is missing its entries.");
                var entries = new ScreenKeysGenerator.Entry[catalog.Screens.Count];
                for (var i = 0; i < entries.Length; i++)
                {
                    var screen = catalog.Screens[i];
                    if (screen == null) throw new InvalidOperationException($"Screen catalog entry {i} is missing.");
                    entries[i] = new ScreenKeysGenerator.Entry(screen.Id, screen.CodeName);
                }

                var source = ScreenKeysGenerator.Generate(catalog.GeneratedNamespace, catalog.GeneratedClassName,
                    catalogGuid, entries);
                var assetPath = EditorUtility.SaveFilePanelInProject("Generate Screen Keys",
                    catalog.GeneratedClassName, "cs", "Save the generated keys in your game's source folder.", "Assets");
                if (string.IsNullOrEmpty(assetPath)) return;

                WriteKeys(assetPath, source, catalogGuid);
                AssetDatabase.ImportAsset(assetPath);
                Debug.Log($"Generated screen keys at {assetPath}.", catalog);
            }
            catch (ArgumentException exception) { ShowError(exception); }
            catch (InvalidOperationException exception) { ShowError(exception); }
            catch (IOException exception) { ShowError(exception); }
            catch (UnauthorizedAccessException exception) { ShowError(exception); }
        }

        private void WriteKeys(string assetPath, string source, string catalogGuid)
        {
            var projectDirectory = Path.GetDirectoryName(Application.dataPath);
            var fullPath = Path.GetFullPath(Path.Combine(projectDirectory, assetPath));
            var assetsDirectory = Path.GetFullPath(Application.dataPath) + Path.DirectorySeparatorChar;
            var scriptPath = AssetDatabase.GetAssetPath(MonoScript.FromScriptableObject(this));
            var packageDirectory = Path.GetFullPath(Path.Combine(projectDirectory, Path.GetDirectoryName(scriptPath), ".."))
                + Path.DirectorySeparatorChar;
            if (!fullPath.StartsWith(assetsDirectory, StringComparison.OrdinalIgnoreCase) ||
                fullPath.StartsWith(packageDirectory, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(Path.GetExtension(fullPath), ".cs", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Save generated C# keys inside your game's Assets folder, outside SimpleUIScreensSystem.");

            if (File.Exists(fullPath) && !ScreenKeysGenerator.IsOwnedByCatalog(File.ReadAllText(fullPath), catalogGuid))
                throw new InvalidOperationException("This file was not generated by this catalog. Choose another file to preserve existing code.");
            File.WriteAllText(fullPath, source, new UTF8Encoding(false));
        }

        private static void ShowError(Exception exception)
        {
            EditorUtility.DisplayDialog("Cannot generate screen keys", exception.Message, "OK");
        }
    }
}
