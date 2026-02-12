using Client.Skit.Skit;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(SkitCharacterEditorUtil))]
public class SkitCharacterEditorUtilInspector : Editor
{
    public override void OnInspectorGUI()
    {
        var obj = (SkitCharacterEditorUtil)target;
        
        if (GUILayout.Button("Copy character position command"))
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