using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// ピボット簡易調整エディタ拡張
/// </summary>
public static class PivotAdjuster
{
    private const string UndoAlignPivot = nameof(UndoAlignPivot);
    
    /// <summary>
    /// ゲームシーン上の選択中のゲームオブジェクトのピボットを調整します.
    /// </summary>
    /// <exception cref="Exception">プレハブはサポートされていません.</exception>
    [MenuItem("GameObject/Adjust Pivot")]
    private static void AdjustPivot()
    {
        if (PrefabUtility.GetPrefabAssetType(Selection.activeTransform) != PrefabAssetType.NotAPrefab)
        {
            throw new Exception("Prefab is not supported.");
        }
        
        // 複数選択対応
        foreach (var activeTransform in Selection.transforms)
        {
            var renderers = activeTransform.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length < 1) throw new Exception("Renderer not found.");
            
            var parent = activeTransform.parent;
            if (parent != null && parent.GetComponents<Component>().Length == 1)
            {
                var children = new List<(Transform, Vector3, Quaternion)>(parent.childCount);
                foreach (Transform child in parent)
                {
                    children.Add((child, child.position, child.rotation));
                }
                parent.position = GetPivotPos(renderers);
                parent.rotation = Quaternion.identity;
                foreach (var (child, pos, rot) in children)
                {
                    child.position = pos;
                    child.rotation = rot;
                }
                Undo.RegisterCompleteObjectUndo(parent.gameObject, UndoAlignPivot);
                return;
            }
            
            var root = new GameObject(activeTransform.name).transform;
            root.position = GetPivotPos(renderers);
            activeTransform.SetParent(root);
            
            Undo.RegisterCreatedObjectUndo(root.gameObject, UndoAlignPivot);
        }
        
        static Vector3 GetPivotPos(in ReadOnlySpan<Renderer> renderers)
        {
            var fullyBounds = renderers[0].bounds;
            foreach (var renderer in renderers[1..])
            {
                fullyBounds.Encapsulate(renderer.bounds);
            }
            var (min, max) = (fullyBounds.min, fullyBounds.max);
            return new Vector3(min.x, max.y, min.z);
        }
    }
}