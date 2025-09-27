using UnityEditor;
using UnityEngine;

namespace AltoEditor
{
    public class EmptyWindow : EditorWindow
    {
        [MenuItem(AltoMenuPath.EditorWindow + "Empty Window")]
        static void ShowWindow()
        {
            var window = CreateInstance<EmptyWindow>();
            window.titleContent = new GUIContent("---");
            window.Show();
        }
    }
}
