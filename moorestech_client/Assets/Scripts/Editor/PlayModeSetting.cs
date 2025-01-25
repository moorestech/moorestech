using Client.Game.GameDebug;
using Client.Starter;
using UnityEditor;
using UnityEngine;

public class PlayModeSetting : EditorWindow
{
    [MenuItem("moorestech/PlayModeSetting")]
    private static void ShowWindow()
    {
        var window = GetWindow<PlayModeSetting>();
        window.titleContent = new GUIContent("PlayModeSetting");
        window.Show();
    }
    
    [SerializeField] private bool isUseOtherServer;
    [SerializeField] private string otherServerDirectory;
    
    private void OnGUI()
    {
        isUseOtherServer = EditorGUILayout.Toggle("Is Use Other Server", isUseOtherServer);
        
        if (isUseOtherServer)
        {
            otherServerDirectory = EditorGUILayout.TextField("Other Server Directory", otherServerDirectory);
            DebugParameters.SaveString(InitializeScenePipeline.OtherDebugServerDirectoryKey, otherServerDirectory);
        }
        else
        {
            DebugParameters.RemoveString(InitializeScenePipeline.OtherDebugServerDirectoryKey);
        }
    }
}