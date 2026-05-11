using Client.Game.InGame.Map.MapVein;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(MapVeinGameObject))]
public class MapVeinGameObjectInspector : Editor
{
    private readonly MapVeinGameObjectEditorService _editorService = new();

    private void OnSceneGUI()
    {
        var mapVein = target as MapVeinGameObject;
        if (mapVein == null) return;

        _editorService.DrawSceneGUI(mapVein.Service, mapVein.Bounds, mapVein.SetBounds, mapVein, Color.red);
    }
}
