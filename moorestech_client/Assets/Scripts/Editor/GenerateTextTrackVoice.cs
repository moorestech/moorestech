using UnityEditor;
using UnityEngine;

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