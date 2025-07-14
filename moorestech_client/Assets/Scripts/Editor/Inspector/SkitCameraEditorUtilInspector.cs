using Client.Skit.Skit;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(SkitCameraEditorUtil))]
public class SkitCameraEditorUtilInspector : Editor
{
    public override void OnInspectorGUI()
    {
        var obj = (SkitCameraEditorUtil)target;
        
        if (GUILayout.Button("カメラワープコマンドをコピー"))
        {
            Vector3 pos = obj.transform.position;
            Vector3 rot = obj.transform.eulerAngles;
            
            string str = $@"[
    {{
        ""type"": ""cameraWarp"",
        ""backgroundColor"": ""#ffffff"",
        ""Position"": [
            {pos.x},
            {pos.y},
            {pos.z}
        ],
        ""Rotation"": [
            {rot.x},
            {rot.y},
            {rot.z}
        ],
        ""id"": 1
    }}
]";
            EditorGUIUtility.systemCopyBuffer = str;  // クリップボードへコピー
        }
        
        base.OnInspectorGUI();
    }
}