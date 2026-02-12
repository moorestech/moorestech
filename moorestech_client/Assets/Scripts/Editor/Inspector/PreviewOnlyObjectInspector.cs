using Client.Game.InGame.BlockSystem.PlaceSystem;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.PreviewObject;
using UnityEditor;

[CustomEditor(typeof(PreviewOnlyObject))]
public class PreviewOnlyObjectInspector : Editor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();
        //TODO i18n対応
        
        EditorGUILayout.HelpBox("This object is only active during placement preview", MessageType.Info);
    }
}
