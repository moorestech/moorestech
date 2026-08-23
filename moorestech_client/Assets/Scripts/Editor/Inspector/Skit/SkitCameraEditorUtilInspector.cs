using Client.Skit.Context;
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
            // スキットJSONは相対座標なのでPlayMode中の実原点で変換する（ADR 0029）
            // Skit JSON is spawn-relative, so convert with the live origin during PlayMode (ADR 0029)
            if (SkitAuthoringOriginResolver.TryResolve(out var origin)) CopyCommand(origin);
        }
        
        base.OnInspectorGUI();
        
        #region Internal
        
        void CopyCommand(SkitOrigin origin)
        {
            var pos = origin.ToRelative(obj.transform.position);
            var rot = obj.transform.eulerAngles;
            
            // Camera コンポーネントを取得
            var cam = obj.GetComponent<Camera>();
            var fov = cam != null ? cam.fieldOfView : 0f;
            
            string str = $@"[
    {{
        ""type"": ""cameraWarp"",
        ""backgroundColor"": ""#ffffff"",
        ""fieldOfView"": {fov},
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
        
        #endregion
    }
}
