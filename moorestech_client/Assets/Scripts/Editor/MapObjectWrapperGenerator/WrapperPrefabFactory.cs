using System;
using System.Collections.Generic;
using System.IO;
using Client.Common;
using Client.Game.InGame.Map.MapObject;
using UnityEditor;
using UnityEngine;

// BKプレハブをネストしたルートへ、アウトライン・レイターゲット・HPバーを足したラッパープレハブを1体分作る
// Builds one wrapper prefab: a nested BK prefab root plus its outline, ray target, and HP bar
public static class WrapperPrefabFactory
{
    private const string HpBarPrefabPath = "Assets/Asset/Environment/Prefab/MapObjectHpBar.prefab";
    private const string OutlineMaterialPath = "Assets/Asset/Common/Shader/Outline/Outline.mat";
    private const string OutlineLayerName = "Outline";
    private const string OutlineObjectName = "Outline";
    private const string RayTargetObjectName = "RayTargetCollider";

    // HPバーが樹冠へ埋まらないよう頂部から離す高さ
    // Height that lifts the HP bar clear of the canopy top
    private const float HpBarHeightMargin = 0.5f;

    public static void CreateWrapperPrefab(MapObjectWrapperSpecies species)
    {
        var sourcePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(species.prefabPath);
        if (sourcePrefab == null) throw new InvalidOperationException($"BK prefab not found: {species.prefabPath}");

        // BKプレハブのインスタンスをルートにするので、保存結果はBKプレハブのバリアントになる（Bush.prefabと同じ形）
        // The root is an instance of the BK prefab, so the saved asset is a variant of it, exactly like Bush.prefab
        var root = (GameObject)PrefabUtility.InstantiatePrefab(sourcePrefab);
        root.name = species.mapObjectName;
        root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

        var localBounds = CalculateLocalBounds(root);
        ApplyMapObjectLayer(root);
        var mapObject = root.AddComponent<MapObjectGameObject>();
        var outlineObject = CreateOutline(root);
        var hpBarView = CreateHpBar(root, localBounds);
        CreateRayTarget(root, localBounds);

        var serializedMapObject = new SerializedObject(mapObject);
        serializedMapObject.FindProperty("outlineObject").objectReferenceValue = outlineObject;
        serializedMapObject.FindProperty("hpBarView").objectReferenceValue = hpBarView;
        serializedMapObject.FindProperty("mapObjectGuid").stringValue = species.mapObjectGuid;
        serializedMapObject.ApplyModifiedPropertiesWithoutUndo();

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(species.wrapperPath)));
        PrefabUtility.SaveAsPrefabAsset(root, species.wrapperPath);
        UnityEngine.Object.DestroyImmediate(root);
    }

    // BKが持つ当たり判定がDefaultレイヤーに残ると設置レイなど汎用レイキャストが樹木へ刺さるので、既存Tree.prefabと同じくMapObjectへ寄せる
    // BK's own colliders left on the Default layer would catch generic raycasts such as block placement, so move them to MapObject like the existing Tree.prefab
    private static void ApplyMapObjectLayer(GameObject root)
    {
        foreach (var child in root.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = LayerConst.MapObjectLayer;
    }

    private static GameObject CreateOutline(GameObject root)
    {
        var outlineMaterial = AssetDatabase.LoadAssetAtPath<Material>(OutlineMaterialPath);
        if (outlineMaterial == null) throw new InvalidOperationException($"outline material not found: {OutlineMaterialPath}");

        var outlineLayer = LayerMask.NameToLayer(OutlineLayerName);
        var outlineRoot = new GameObject(OutlineObjectName) { layer = outlineLayer };
        outlineRoot.transform.SetParent(root.transform, false);

        foreach (var sourceRenderer in CollectNearestLodRenderers(root))
        {
            var sourceFilter = sourceRenderer.GetComponent<MeshFilter>();
            if (sourceFilter == null || sourceFilter.sharedMesh == null) continue;

            var outlineMesh = new GameObject(sourceRenderer.name) { layer = outlineLayer };
            outlineMesh.transform.SetParent(outlineRoot.transform, false);
            CopyWorldTransform(sourceRenderer.transform, outlineMesh.transform);

            outlineMesh.AddComponent<MeshFilter>().sharedMesh = sourceFilter.sharedMesh;
            outlineMesh.AddComponent<MeshRenderer>().sharedMaterials = FillOutlineMaterials(sourceRenderer.sharedMaterials.Length, outlineMaterial);
        }

        if (outlineRoot.transform.childCount == 0) throw new InvalidOperationException($"no outline mesh could be built for {root.name}");

        // フォーカス時にMapObjectGameObjectが点ける
        // MapObjectGameObject turns this on while the object is focused
        outlineRoot.SetActive(false);
        return outlineRoot;
    }

    private static MapObjectHpBarView CreateHpBar(GameObject root, Bounds localBounds)
    {
        var hpBarPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(HpBarPrefabPath);
        if (hpBarPrefab == null) throw new InvalidOperationException($"hp bar prefab not found: {HpBarPrefabPath}");

        // MapObjectGameObjectが親のlossyScaleで逆スケール補正するため、HPバーはルート直下に置く
        // MapObjectGameObject counter-scales the bar by its parent lossyScale, so it must sit directly under the root
        var hpBar = (GameObject)PrefabUtility.InstantiatePrefab(hpBarPrefab, root.transform);
        hpBar.transform.localPosition = new Vector3(0f, localBounds.max.y + HpBarHeightMargin, 0f);

        var hpBarView = hpBar.GetComponent<MapObjectHpBarView>();
        if (hpBarView == null) throw new InvalidOperationException($"hp bar prefab has no MapObjectHpBarView: {HpBarPrefabPath}");
        return hpBarView;
    }

    private static void CreateRayTarget(GameObject root, Bounds localBounds)
    {
        // 採掘のレイキャストはMapObjectレイヤーだけを見るので、当たり判定はこのレイヤーに置く
        // The mining raycast only sees the MapObject layer, so the hit box lives there
        var rayTarget = new GameObject(RayTargetObjectName) { layer = LayerConst.MapObjectLayer };
        rayTarget.transform.SetParent(root.transform, false);

        var boxCollider = rayTarget.AddComponent<BoxCollider>();
        boxCollider.isTrigger = true;
        boxCollider.center = localBounds.center;
        boxCollider.size = localBounds.size;

        rayTarget.AddComponent<MapObjectRayTarget>();
    }

    // 見た目の外接をルートのローカル空間で求める。ルートに拡縮が入っていても子のlocalPositionへそのまま渡せる
    // Computes the visual bounds in the root's local space so it can be handed straight to a child's local transform even when the root is scaled
    private static Bounds CalculateLocalBounds(GameObject root)
    {
        var renderers = root.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) throw new InvalidOperationException($"no renderer under {root.name}");

        var worldToLocal = root.transform.worldToLocalMatrix;
        var localBounds = new Bounds(worldToLocal.MultiplyPoint3x4(renderers[0].bounds.center), Vector3.zero);
        foreach (var renderer in renderers)
        {
            var worldBounds = renderer.bounds;
            for (var cornerIndex = 0; cornerIndex < 8; cornerIndex++)
            {
                var corner = new Vector3(
                    (cornerIndex & 1) == 0 ? worldBounds.min.x : worldBounds.max.x,
                    (cornerIndex & 2) == 0 ? worldBounds.min.y : worldBounds.max.y,
                    (cornerIndex & 4) == 0 ? worldBounds.min.z : worldBounds.max.z);
                localBounds.Encapsulate(worldToLocal.MultiplyPoint3x4(corner));
            }
        }

        return localBounds;
    }

    // 最近接LODのレンダラーだけをアウトライン化する。遠景LODまで複製すると輪郭が二重になる
    // Only the nearest LOD is outlined; duplicating the far LODs too would double the silhouette
    private static List<Renderer> CollectNearestLodRenderers(GameObject root)
    {
        var renderers = new List<Renderer>();
        var lodGroup = root.GetComponentInChildren<LODGroup>();
        if (lodGroup == null)
        {
            renderers.AddRange(root.GetComponentsInChildren<MeshRenderer>());
            return renderers;
        }

        foreach (var renderer in lodGroup.GetLODs()[0].renderers)
            if (renderer != null)
                renderers.Add(renderer);
        return renderers;
    }

    private static void CopyWorldTransform(Transform source, Transform target)
    {
        target.SetPositionAndRotation(source.position, source.rotation);

        // localScaleは親の合成拡縮を打ち消してから入れる
        // Cancel out the parent's accumulated scale before assigning localScale
        var parentScale = target.parent.lossyScale;
        var sourceScale = source.lossyScale;
        target.localScale = new Vector3(sourceScale.x / parentScale.x, sourceScale.y / parentScale.y, sourceScale.z / parentScale.z);
    }

    private static Material[] FillOutlineMaterials(int sourceMaterialCount, Material outlineMaterial)
    {
        // サブメッシュごとにスロットが要るので、元と同数（最低1枚）を全部アウトラインで埋める
        // Every submesh needs a slot, so fill as many as the source had, never fewer than one
        var materials = new Material[Mathf.Max(1, sourceMaterialCount)];
        for (var index = 0; index < materials.Length; index++) materials[index] = outlineMaterial;
        return materials;
    }
}
