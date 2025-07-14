using Client.Skit.Skit;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(SkitObjectTransfomCopyer))]
public class SkitObjectTransfomCopyerInspector : Editor
{
    public override void OnInspectorGUI()
    {
        var obj = (SkitObjectTransfomCopyer)target;
        
        if (GUILayout.Button("キャラ位置設定コマンドをコピー"))
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