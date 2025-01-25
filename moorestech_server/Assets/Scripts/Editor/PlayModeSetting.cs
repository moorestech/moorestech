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
        
        var serverDirectory = DebugParameters.GetValueOrDefaultString(StartServer.DebugServerDirectorySettingKey, "");
        window.isUseOtherServer = !string.IsNullOrEmpty(serverDirectory);
        window.otherServerDirectory = serverDirectory;
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
            if (string.IsNullOrEmpty(otherServerDirectory))
            {
                DebugParameters.RemoveString(key);
            }
            else
            {
                DebugParameters.SaveString(key, otherServerDirectory);
            }
        }
        else
        {
            DebugParameters.RemoveString(key);
        }
    }
}