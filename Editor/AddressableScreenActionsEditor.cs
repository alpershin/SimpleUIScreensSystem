using System.Collections.Generic;
using SimpleUIScreensSystem.AddressableUI;
using UnityEditor;
using UnityEngine;

namespace SimpleUIScreensSystem.Editor
{
    [CustomEditor(typeof(AddressableScreenActions))]
    internal sealed class AddressableScreenActionsEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            var navigatorProperty = serializedObject.FindProperty("_navigator");
            var idProperty = serializedObject.FindProperty("_screenId");
            EditorGUILayout.PropertyField(navigatorProperty);

            var navigator = navigatorProperty.objectReferenceValue as AddressableUIRoot;
            if (navigator == null || navigator.Catalog == null || navigator.Catalog.Screens == null)
            {
                EditorGUILayout.HelpBox("Assign a navigator with a screen catalog to select a screen.", MessageType.Info);
                serializedObject.ApplyModifiedProperties();
                return;
            }

            var labels = new List<string> { "None" };
            var ids = new List<string> { string.Empty };
            var current = 0;
            foreach (var screen in navigator.Catalog.Screens)
            {
                if (screen == null || !screen.Id.IsValid) continue;
                ids.Add(screen.Id.Value);
                labels.Add(string.IsNullOrWhiteSpace(screen.CodeName) ? screen.Id.Value : screen.CodeName);
                if (screen.Id.Value == idProperty.stringValue) current = ids.Count - 1;
            }
            if (current == 0 && !string.IsNullOrEmpty(idProperty.stringValue))
            {
                current = ids.Count;
                labels.Add("Missing: " + idProperty.stringValue);
                ids.Add(idProperty.stringValue);
                EditorGUILayout.HelpBox("The saved screen key is missing from this catalog. Select a replacement.", MessageType.Warning);
            }

            EditorGUI.BeginChangeCheck();
            var selected = EditorGUILayout.Popup("Screen", current, labels.ToArray());
            if (EditorGUI.EndChangeCheck()) idProperty.stringValue = ids[selected];
            serializedObject.ApplyModifiedProperties();
        }
    }
}
