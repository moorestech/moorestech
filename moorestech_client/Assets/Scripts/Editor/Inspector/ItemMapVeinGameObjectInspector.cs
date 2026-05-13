using Client.Game.InGame.Map.MapVein;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ItemMapVeinGameObject))]
public class ItemMapVeinGameObjectInspector : Editor
{
    private readonly MapVeinGameObjectEditorService _editorService = new();

    private void OnSceneGUI()
    {
        var mapVein = target as ItemMapVeinGameObject;
        if (mapVein == null) return;

        _editorService.DrawSceneGUI(mapVein.Service, mapVein.Bounds, mapVein.SetBounds, mapVein, Color.red);
    }
}
