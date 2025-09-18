using System;
using UnityEngine;
using UnityEditor;

/// <summary>
/// インスペクタ上部に、そのアセットを Project View でハイライトするボタンを表示
/// </summary>
namespace AltoEditor
{
    [InitializeOnLoad]
    public static class PingFromInspector
    {
        const string MenuPath = AltoMenuPath.EditorExt + "Ping Button From Inspector";
        static string PrefsKey => "AltoEditor-PingFromInspector";

        [MenuItem(MenuPath)]
        static void ToggleEnabled()
        {
            bool isChecked = !Menu.GetChecked(MenuPath);
            Menu.SetChecked(MenuPath, isChecked);
            EditorSettingsUtil.SaveBool(PrefsKey, isChecked);
        }

        [MenuItem(MenuPath, true)]
        static bool Remember()
        {
            bool isChecked = EditorSettingsUtil.LoadBool(PrefsKey, false);
            Menu.SetChecked(MenuPath, isChecked);
            return true;
        }

        static PingFromInspector()
        {
            EditorApplication.delayCall += () => Remember();
            Editor.finishedDefaultHeaderGUI += editor =>
            {
                if (!Menu.GetChecked(MenuPath)) { return; }

                var path = AssetDatabase.GetAssetPath(editor.target);
                if (String.IsNullOrEmpty(path)) { return; }

                GUILayout.BeginHorizontal();
                {
                    if (GUILayout.Button("Ping", GUILayout.MaxWidth(90)))
                    {
                        var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
                        EditorGUIUtility.PingObject(asset);
                    }
                    if (GUILayout.Button("Copy Path", GUILayout.MaxWidth(120)))
                    {
                        EditorGUIUtility.systemCopyBuffer = path;
                    }
                }
                GUILayout.EndHorizontal();

                EditorGUILayout.HelpBox(path, MessageType.Info);
            };
        }
    }
}
