using UnityEditor;
using UnityEngine;


public class GenerateTextTrackVoice : EditorWindow
{
    [MenuItem("moorestech/GenerateTextTrackVoice")]
    private static void ShowWindow()
    {
        var window = GetWindow<GenerateTextTrackVoice>();
        window.titleContent = new GUIContent("GenerateTextTrackVoice");
        window.Show();
    }

    private void CreateGUI()
    {
    }
}