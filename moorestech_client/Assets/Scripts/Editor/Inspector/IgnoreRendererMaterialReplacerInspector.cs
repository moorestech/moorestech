using Client.Game.InGame.Block;
using UnityEditor;

[CustomEditor(typeof(IgnoreRendererMaterialReplacer))]
public class IgnoreRendererMaterialReplacerInspector : Editor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();
        //TODO i18n対応
        
        EditorGUILayout.HelpBox("このコンポーネント以下のレンダラーはマテリアル置き換えされません", MessageType.Info);
    }
}