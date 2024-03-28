using Client.Story;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(StoryObject))]
public class StoryObjectInspector : Editor
{
    public override void OnInspectorGUI()
    {
        var obj = target as StoryObject;
        if (GUILayout.Button("座標と角度をコピー"))
        {
            //クリップボードに座標と角度のTSVをコピー
            var pos = obj.transform.position;
            var rot = obj.transform.eulerAngles;
            var str = $"pos\t{pos.x}\t{pos.y}\t{pos.z}\trot\t{rot.x}\t{rot.y}\t{rot.z}";
            EditorGUIUtility.systemCopyBuffer = str;
        }
    }
}