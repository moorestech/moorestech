using Client.Game.InGame.BlockSystem.PlaceSystem;
using UnityEditor;

[CustomEditor(typeof(PreviewOnlyObject))]
public class PreviewOnlyObjectInspector : Editor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();
        //TODO i18n対応
        
        EditorGUILayout.HelpBox("このオブジェクトは設置プレビュー時のみオンになります", MessageType.Info);
    }
}
