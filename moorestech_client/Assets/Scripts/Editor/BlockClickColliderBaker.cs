using System.Collections.Generic;
using Client.Common;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.PreviewObject;
using Client.Game.InGame.Context;
using UnityEditor;
using UnityEngine;

/// <summary>
///     クリック可能Colliderを持たないブロックプレハブへ、描画境界サイズのBoxCollider子を焼き込むツール。
///     実行時の自動Collider付与(9e1751462で廃止)を復活させず、アセット側で当たり判定を完結させるために使う。
///     Bakes a renderer-bounds-sized BoxCollider child into block prefabs that have no clickable collider,
///     so hit detection lives in assets without resurrecting the runtime auto-attachment removed in 9e1751462.
/// </summary>
public static class BlockClickColliderBaker
{
    private const string BlockPrefabRootPath = "Assets/AddressableResources/Block";
    private const string ExcludedUtilPath = BlockPrefabRootPath + "/Util";
    private const string ClickColliderObjectName = "ClickCollider";

    [MenuItem("moorestech/Bake Block Click Colliders")]
    public static void Bake()
    {
        var baked = new List<string>();
        var skippedNoRenderer = new List<string>();
        var alreadyClickable = 0;

        foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { BlockPrefabRootPath }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);

            // Util配下はブロックとして設置されないプレビュー/コネクタ用のため対象外
            // Skip Util: preview/connector prefabs that are never placed as blocks
            if (path.StartsWith(ExcludedUtilPath)) continue;

            var root = PrefabUtility.LoadPrefabContents(path);
            if (BakeClickCollider(root, out var reason))
            {
                PrefabUtility.SaveAsPrefabAsset(root, path);
                baked.Add(System.IO.Path.GetFileNameWithoutExtension(path));
            }
            else if (reason == SkipReason.NoRenderer)
            {
                skippedNoRenderer.Add(System.IO.Path.GetFileNameWithoutExtension(path));
            }
            else
            {
                alreadyClickable++;
            }
            PrefabUtility.UnloadPrefabContents(root);
        }

        Debug.Log($"[ClickColliderBake] baked={baked.Count} alreadyClickable={alreadyClickable} noRenderer={skippedNoRenderer.Count}");
        Debug.Log("[ClickColliderBake] baked:\n" + string.Join("\n", baked));
        Debug.Log("[ClickColliderBake] noRenderer(unbakeable):\n" + string.Join("\n", skippedNoRenderer));
    }

    private enum SkipReason
    {
        None,
        AlreadyClickable,
        NoRenderer,
    }

    private static bool BakeClickCollider(GameObject root, out SkipReason reason)
    {
        // 既にクリック可能Colliderがある、または既に焼き込み済みなら何もしない
        // Do nothing when a clickable collider already exists or the collider is already baked
        var blockRoot = root.transform;
        foreach (var collider in root.GetComponentsInChildren<Collider>(true))
        {
            if (BlockGameObjectColliderSetup.IsClickableCollider(blockRoot, collider))
            {
                reason = SkipReason.AlreadyClickable;
                return false;
            }
        }

        if (!TryCalcVisualBounds(blockRoot, out var visualBounds))
        {
            reason = SkipReason.NoRenderer;
            return false;
        }

        // 描画境界サイズのBoxColliderをBlockレイヤーの子として追加する
        // Add a renderer-bounds-sized BoxCollider as a Block-layer child
        var clickColliderObject = new GameObject(ClickColliderObjectName)
        {
            layer = LayerConst.BlockLayer,
        };
        clickColliderObject.transform.SetParent(blockRoot);
        clickColliderObject.transform.SetPositionAndRotation(visualBounds.center, Quaternion.identity);

        var lossyScale = clickColliderObject.transform.lossyScale;
        var boxCollider = clickColliderObject.AddComponent<BoxCollider>();
        boxCollider.size = new Vector3(visualBounds.size.x / lossyScale.x, visualBounds.size.y / lossyScale.y, visualBounds.size.z / lossyScale.z);

        reason = SkipReason.None;
        return true;
    }

    private static bool TryCalcVisualBounds(Transform blockRoot, out Bounds bounds)
    {
        // プレビュー専用・非アクティブを除いたMesh/SkinnedMeshRendererの境界を合成する
        // Combine MeshRenderer/SkinnedMeshRenderer bounds, excluding preview-only and inactive subtrees
        bounds = new Bounds();
        var found = false;
        foreach (var renderer in blockRoot.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer is not (MeshRenderer or SkinnedMeshRenderer)) continue;
            if (!BlockGameObjectColliderSetup.IsActiveAndNotPreview(blockRoot, renderer.transform)) continue;

            if (!found)
            {
                bounds = renderer.bounds;
                found = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }
        return found;
    }
}
