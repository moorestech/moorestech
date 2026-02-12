using Client.Game.InGame.Block;
using UnityEditor;

[CustomEditor(typeof(IgnoreRendererMaterialReplacer))]
public class IgnoreRendererMaterialReplacerInspector : Editor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();
        //TODO i18n対応
        
        EditorGUILayout.HelpBox("Renderers under this component will not have materials replaced", MessageType.Info);
    }
}