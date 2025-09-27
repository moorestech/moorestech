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
            var pos = obj.transform.position;
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
        
        base.OnInspectorGUI();
    }
}