using System.Collections.Generic;
using System.Linq;
using Client.Common;
using Client.Game.InGame.Context;
using UnityEditor;
using UnityEngine;

/// <summary>
///     手付けのクリック可能Colliderを持たないブロックプレハブへ、描画境界サイズのBoxCollider子「ClickCollider」を焼き込むツール。
///     判定はベイク産ClickColliderを除いた手付けColliderのみで行い、ネストプレハブから伝播したClickColliderは親側で無効化する。
///     実行時の自動Collider付与(9e1751462で廃止)を復活させず、アセット側で当たり判定を完結させるために使う。
///     Bakes a renderer-bounds-sized "ClickCollider" BoxCollider child into block prefabs that lack hand-authored
///     clickable colliders. Baked ClickColliders are excluded from the skip judgement, and ClickColliders propagated
///     from nested prefabs are disabled in the outer prefab, keeping hit detection asset-side without runtime attachment.
/// </summary>
public static class BlockClickColliderBaker
{
    private const string BlockPrefabRootPath = "Assets/AddressableResources/Block";
    private const string ExcludedUtilPath = BlockPrefabRootPath + "/Util";
    private const string ClickColliderObjectName = "ClickCollider";

    [MenuItem("moorestech/Bake Block Click Colliders")]
    public static void Bake()
    {
        var paths = AssetDatabase.FindAssets("t:Prefab", new[] { BlockPrefabRootPath })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(p => !p.StartsWith(ExcludedUtilPath))
            .ToList();

        // パス1: 各プレハブ自身の焼き込み（自前ClickColliderは作り直し、手付けCollider持ちはスキップ）
        // Pass 1: bake each prefab's own box (recreate own ClickCollider; skip prefabs with authored colliders)
        var baked = new List<string>();
        var authoredSkipped = 0;
        var noRenderer = new List<string>();
        foreach (var path in paths)
        {
            var result = BakeOwnClickCollider(path);
            if (result == BakeResult.Baked) baked.Add(FileName(path));
            else if (result == BakeResult.NoRenderer) noRenderer.Add(FileName(path));
            else authoredSkipped++;
        }

        // パス2: 全プレハブ保存後に、ネスト由来のClickColliderを親側で無効化（順序非依存にするため分離）
        // Pass 2: after all saves, disable nested ClickColliders in outer prefabs (separated to be order-independent)
        var nestedDisabled = new List<string>();
        foreach (var path in paths)
        {
            if (DisableNestedClickColliders(path)) nestedDisabled.Add(FileName(path));
        }

        Debug.Log($"[ClickColliderBake] baked={baked.Count} authoredSkipped={authoredSkipped} noRenderer={noRenderer.Count} nestedDisabled={nestedDisabled.Count}");
        Debug.Log("[ClickColliderBake] baked:\n" + string.Join("\n", baked));
        Debug.Log("[ClickColliderBake] nestedDisabled:\n" + string.Join("\n", nestedDisabled));
        Debug.Log("[ClickColliderBake] noRenderer(unbakeable):\n" + string.Join("\n", noRenderer));
    }

    private enum BakeResult
    {
        Baked,
        AuthoredColliderExists,
        NoRenderer,
    }

    private static BakeResult BakeOwnClickCollider(string path)
    {
        var root = PrefabUtility.LoadPrefabContents(path);
        var result = ExecuteBake();
        if (result == BakeResult.Baked) PrefabUtility.SaveAsPrefabAsset(root, path);
        PrefabUtility.UnloadPrefabContents(root);
        return result;

        #region Internal

        BakeResult ExecuteBake()
        {
            // 自前（非ネスト）の旧ClickColliderを削除し、再生成できるようにする
            // Remove own (non-nested) previously-baked ClickColliders so they can be regenerated
            var ownOldColliders = root.GetComponentsInChildren<Transform>(true)
                .Where(t => t.name == ClickColliderObjectName && !PrefabUtility.IsPartOfPrefabInstance(t.gameObject))
                .ToList();
            foreach (var old in ownOldColliders) Object.DestroyImmediate(old.gameObject);

            // 手付けのクリック可能Colliderがあれば焼き込まない（ベイク産ClickColliderは判定から除外）
            // Skip when a hand-authored clickable collider exists (baked ClickColliders are excluded from judgement)
            var blockRoot = root.transform;
            var hasAuthoredClickable = root.GetComponentsInChildren<Collider>(true)
                .Any(c => c.gameObject.name != ClickColliderObjectName && BlockGameObjectColliderSetup.IsClickableCollider(blockRoot, c));
            if (hasAuthoredClickable)
            {
                // 旧ClickColliderを消しただけの場合も保存して反映する
                // Save even when the only change is removing old ClickColliders
                if (ownOldColliders.Count > 0) PrefabUtility.SaveAsPrefabAsset(root, path);
                return BakeResult.AuthoredColliderExists;
            }

            if (!TryCalcVisualBounds(blockRoot, out var visualBounds)) return BakeResult.NoRenderer;

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
            return BakeResult.Baked;
        }

        #endregion
    }

    private static bool DisableNestedClickColliders(string path)
    {
        // ネストプレハブから伝播したClickColliderは外側プレハブの描画状態と一致しないため無効化する
        // Disable ClickColliders propagated from nested prefabs since they don't match the outer prefab's visuals
        var root = PrefabUtility.LoadPrefabContents(path);
        var changed = false;
        foreach (var collider in root.GetComponentsInChildren<Collider>(true))
        {
            if (collider.gameObject.name != ClickColliderObjectName) continue;
            if (!PrefabUtility.IsPartOfPrefabInstance(collider.gameObject)) continue;
            if (!collider.enabled) continue;
            collider.enabled = false;
            changed = true;
        }
        if (changed) PrefabUtility.SaveAsPrefabAsset(root, path);
        PrefabUtility.UnloadPrefabContents(root);
        return changed;
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

    private static string FileName(string path)
    {
        return System.IO.Path.GetFileNameWithoutExtension(path);
    }
}
