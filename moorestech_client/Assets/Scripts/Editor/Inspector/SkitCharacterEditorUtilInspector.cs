using Client.Skit.Context;
using Client.Skit.Skit;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(SkitCharacterEditorUtil))]
public class SkitCharacterEditorUtilInspector : Editor
{
    public override void OnInspectorGUI()
    {
        var obj = (SkitCharacterEditorUtil)target;
        
        if (GUILayout.Button("キャラ位置設定コマンドをコピー"))
        {
            // スキットJSONは相対座標なのでPlayMode中の実原点で変換する（ADR 0029）
            // Skit JSON is spawn-relative, so convert with the live origin during PlayMode (ADR 0029)
            if (!SkitAuthoringOriginResolver.TryResolve(out var origin))
            {
                return;
            }
            
            var pos = origin.ToRelative(obj.transform.position);
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
