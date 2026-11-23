using Client.Game.InGame.Block;
using UnityEditor;

[CustomEditor(typeof(IgnoreRendererMaterialReplacer))]
public class IgnoreRendererMaterialReplacerInspector : Editor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();
        //TODO i18n対応
        
        EditorGUILayout.HelpBox("Material Slots が空なら、このコンポーネント以下のレンダラーはマテリアル置き換えされません。指定すると、同じオブジェクトのレンダラーの指定した枠だけ置き換えされません", MessageType.Info);
    }
}
