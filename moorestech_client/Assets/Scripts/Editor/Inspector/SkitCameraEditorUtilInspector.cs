using Client.Skit.Skit;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(SkitCameraEditorUtil))]
public class SkitCameraEditorUtilInspector : Editor
{
    public override void OnInspectorGUI()
    {
        var obj = (SkitCharacterEditorUtil)target;
        
        if (GUILayout.Button("カメラワープコマンドをコピー"))
        {
            var pos = obj.transform.position;
            var rot = obj.transform.eulerAngles;
            
            var str = $@"[
    {{
        ""type"": ""characterTransform"",
        ""backgroundColor"": ""#ffffff"",
        ""character"": ""{obj.characterId}"",
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
            EditorGUIUtility.systemCopyBuffer = str;   // クリップボードへコピー
        }
        
        base.OnInspectorGUI();
    }
}