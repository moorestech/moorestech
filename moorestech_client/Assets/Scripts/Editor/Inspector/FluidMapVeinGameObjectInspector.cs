using Client.Game.InGame.Map.MapVein;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

[CustomEditor(typeof(FluidMapVeinGameObject))]
public class FluidMapVeinGameObjectInspector : Editor
{
    private readonly BoxBoundsHandle _boxBoundsHandle = new();

    private void OnSceneGUI()
    {
        var fluidVein = target as FluidMapVeinGameObject;
        if (fluidVein == null)
        {
            return;
        }

        EditorGUI.BeginChangeCheck();

        _boxBoundsHandle.center = fluidVein.Bounds.center + fluidVein.transform.position;
        _boxBoundsHandle.size = fluidVein.Bounds.size;

        _boxBoundsHandle.SetColor(Color.blue);
        _boxBoundsHandle.DrawHandle();

        if (EditorGUI.EndChangeCheck())
        {
            var bounds = new Bounds(_boxBoundsHandle.center, _boxBoundsHandle.size);
            fluidVein.SetBounds(bounds);
            Undo.RecordObject(fluidVein, "Change Bounds");
            EditorUtility.SetDirty(fluidVein);
        }
    }
}
