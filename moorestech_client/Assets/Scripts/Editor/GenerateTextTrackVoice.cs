using UnityEditor;
using UnityEngine;

namespace Editor
{
    public class GenerateTextTrackVoice : EditorWindow
    {
        private void CreateGUI()
        {
        }
        [MenuItem("moorestech/GenerateTextTrackVoice")]
        private static void ShowWindow()
        {
            var window = GetWindow<GenerateTextTrackVoice>();
            window.titleContent = new GUIContent("GenerateTextTrackVoice");
            window.Show();
        }
    }
}