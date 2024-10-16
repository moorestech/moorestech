using System;
using Client.Game.InGame.Map.MapVein;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

[CustomEditor(typeof(MapVeinGameObject))]
public class MapVeinGameObjectInspector : Editor
{
    private readonly BoxBoundsHandle _boxBoundsHandle = new();
    
    private void OnSceneGUI()
    {
        var mapVein = target as MapVeinGameObject;
        if (mapVein == null)
        {
            return;
        }
        
        EditorGUI.BeginChangeCheck();
        
        _boxBoundsHandle.center = mapVein.Bounds.center + mapVein.transform.position;
        _boxBoundsHandle.size = mapVein.Bounds.size;
        
        _boxBoundsHandle.SetColor(Color.red);
        _boxBoundsHandle.DrawHandle();
        
        if (EditorGUI.EndChangeCheck())
        {
            
            var bounds = new Bounds(_boxBoundsHandle.center, _boxBoundsHandle.size);
            mapVein.SetBounds(bounds);
            Undo.RecordObject(mapVein, "Change Bounds");
        }
    }
}