using System;
using Client.Game.InGame.Map.MapVein;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using Object = UnityEngine.Object;

// SceneGUIのBoxBoundsHandle操作とUndo登録の共通処理
// Common SceneGUI logic for MapVein objects: BoxBoundsHandle manipulation and Undo registration
public class MapVeinGameObjectEditorService
{
    private readonly BoxBoundsHandle _handle = new();

    public void DrawSceneGUI(MapVeinGameObjectService service, Bounds bounds, Action<Bounds> setBoundsAction, Object undoTarget, Color color)
    {
        EditorGUI.BeginChangeCheck();

        _handle.center = bounds.center + service.Transform.position;
        _handle.size = bounds.size;
        _handle.SetColor(color);
        _handle.DrawHandle();

        if (EditorGUI.EndChangeCheck())
        {
            // ハンドル中心はワールド座標なのでローカル空間に戻して保存
            // Convert handle's world-space center back to local space before storing
            setBoundsAction(new Bounds(_handle.center - service.Transform.position, _handle.size));
            
            Undo.RecordObject(undoTarget, "Change Bounds");
            EditorUtility.SetDirty(undoTarget);
        }
    }
}
