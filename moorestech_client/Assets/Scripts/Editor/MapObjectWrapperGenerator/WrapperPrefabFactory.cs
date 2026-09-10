using System;
using System.Collections.Generic;
using System.IO;
using Client.Common;
using Client.Game.InGame.Interact.Outline;
using Client.Game.InGame.Map.MapObject;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

// BKへアウトライン・レイターゲット・HPバーを付与
// Builds one wrapper prefab: BK plus its outline, ray target, and HP bar
public static class WrapperPrefabFactory
{
    private const string HpBarPrefabPath = "Assets/Asset/Environment/Prefab/MapObjectHpBar.prefab";
    private const string OutlineMaterialPath = "Assets/Resources/InteractOutline.mat";

    // HPバーが樹冠へ埋まらないよう頂部から離す高さ
    // Height that lifts the HP bar clear of the canopy top
    private const float HpBarHeightMargin = 0.5f;

    // kind値。HPバー分岐に唯一使う
    // The kind value; the sole consumer that branches HP bar necessity on it
    private const string PebbleKind = "pebble";

    public static void CreateWrapperPrefab(MapObjectWrapperSpecies species, Scene workScene)
    {
        var sourcePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(species.prefabPath);
        if (sourcePrefab == null) throw new InvalidOperationException($"BK prefab not found: {species.prefabPath}");
        if (LayerConst.MapObjectLayer < 0 || LayerConst.OutlineLayer < 0) throw new InvalidOperationException("MapObject or Outline layer is not defined in this project");

        // BKプレハブのインスタンスをルートにするので、保存結果はBKプレハブのバリアントになる（Bush.prefabと同じ形）
        // The root is an instance of the BK prefab, so the saved asset is a variant of it, exactly like Bush.prefab
        var root = (GameObject)PrefabUtility.InstantiatePrefab(sourcePrefab, workScene);
        root.name = species.mapObjectName;
        root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

        var localBounds = CalculateLocalBounds(root);
        var nearestLodRenderers = RuntimeOutlineFactory.CollectNearestLodRenderers(root);
        ApplyMapObjectLayer(root);
        var mapObject = root.AddComponent<MapObjectGameObject>();
        var outlineObject = CreateOutline(root);

        // レイターゲットはBKの当たり判定を測るので、後から足す物を巻き込まないようHPバーより先に作る
        // The ray target measures BK's own colliders, so it is built before the HP bar and never sees anything added later
        WrapperRayTargetBuilder.Create(root, localBounds, nearestLodRenderers);

        // HPバーの高さは見た目の外接、レイターゲットは幹の太さと、参照する外接を分ける
        // The HP bar rides the visual bounds while the ray target follows the trunk, so the two use different bounds
        // PickUp種(小石)は既存Pebble.prefab前例に倣いHPバーを持たない（1操作で消えるのでHP表示が不要）
        // PickUp species (pebbles) carry no HP bar, matching the existing Pebble.prefab precedent (they vanish in one action, so HP display is meaningless)
        var hpBarView = species.kind == PebbleKind ? null : CreateHpBar(root, localBounds);

        var serializedMapObject = new SerializedObject(mapObject);
        serializedMapObject.FindProperty("outlineObject").objectReferenceValue = outlineObject;
        serializedMapObject.FindProperty("hpBarView").objectReferenceValue = hpBarView;
        serializedMapObject.FindProperty("mapObjectGuid").stringValue = species.mapObjectGuid;
        serializedMapObject.ApplyModifiedPropertiesWithoutUndo();

        // 静的mapObjectは動的probe補間を使わず、全Rendererを同じauthoring規則へ揃える
        // Static map objects skip dynamic probe interpolation, applying one authoring rule to every Renderer
        DisableProbeSampling(root);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(species.wrapperPath)));
        PrefabUtility.SaveAsPrefabAsset(root, species.wrapperPath);
        UnityEngine.Object.DestroyImmediate(root);
    }

    private static void DisableProbeSampling(GameObject root)
    {
        foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }
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

        // 実行時ハイライトと同じ手順で焼き込む。焼き込みでは輪郭が無いプレハブを出荷できないので失敗は例外にする
        // Bakes through the same procedure the runtime highlight uses; a prefab without an outline must never ship, so a failure throws here
        var outlineRoot = RuntimeOutlineFactory.Create(root, outlineMaterial);
        if (outlineRoot == null) throw new InvalidOperationException($"no outline mesh could be built for {root.name}");

        return outlineRoot;
    }

    private static MapObjectHpBarView CreateHpBar(GameObject root, Bounds localBounds)
    {
        var hpBarPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(HpBarPrefabPath);
        if (hpBarPrefab == null) throw new InvalidOperationException($"hp bar prefab not found: {HpBarPrefabPath}");

        // HPバーの高さ(localBounds.max.y)はルートローカル空間の値なので、ルート直下に置いてそのまま渡す
        // The HP bar height (localBounds.max.y) is expressed in the root's local space, so it sits directly under the root to consume that value as-is
        var hpBar = (GameObject)PrefabUtility.InstantiatePrefab(hpBarPrefab, root.transform);
        hpBar.transform.localPosition = new Vector3(0f, localBounds.max.y + HpBarHeightMargin, 0f);

        var hpBarView = hpBar.GetComponent<MapObjectHpBarView>();
        if (hpBarView == null) throw new InvalidOperationException($"hp bar prefab has no MapObjectHpBarView: {HpBarPrefabPath}");
        return hpBarView;
    }

    // 見た目の外接をルートのローカル空間で求める。ルートに拡縮が入っていても子のlocalPositionへそのまま渡せる
    // Computes the visual bounds in the root's local space so it can be handed straight to a child's local transform even when the root is scaled
    private static Bounds CalculateLocalBounds(GameObject root)
    {
        var localBounds = new WrapperLocalBoundsAccumulator(root.transform);
        foreach (var renderer in root.GetComponentsInChildren<Renderer>(true)) localBounds.AddWorldBounds(renderer.bounds);
        if (!localBounds.HasPoint) throw new InvalidOperationException($"no renderer under {root.name}");

        return localBounds.GetBounds();
    }
}
