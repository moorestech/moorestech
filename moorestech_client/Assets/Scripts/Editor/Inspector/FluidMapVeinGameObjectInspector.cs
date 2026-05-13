using Client.Game.InGame.Map.MapVein;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(FluidMapVeinGameObject))]
public class FluidMapVeinGameObjectInspector : Editor
{
    private readonly MapVeinGameObjectEditorService _editorService = new();

    private void OnSceneGUI()
    {
        var fluidVein = target as FluidMapVeinGameObject;
        if (fluidVein == null) return;

        _editorService.DrawSceneGUI(fluidVein.Service, fluidVein.Bounds, fluidVein.SetBounds, fluidVein, Color.blue);
    }
}
