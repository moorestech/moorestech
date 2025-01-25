using Common.Debug;
using Server.Boot;
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
        
        const string key = StartServer.DebugServerDirectorySettingKey;
        if (isUseOtherServer)
        {
            otherServerDirectory = EditorGUILayout.TextField("Other Server Directory", otherServerDirectory);
            DebugParameters.SaveString(key, otherServerDirectory);
        }
        else
        {
            DebugParameters.RemoveString(key);
        }
    }
}