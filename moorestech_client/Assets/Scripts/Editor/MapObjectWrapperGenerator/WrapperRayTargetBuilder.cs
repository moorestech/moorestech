using System;
using System.Collections.Generic;
using Client.Common;
using Client.Game.InGame.Map.MapObject;
using UnityEngine;
using UnityEngine.SceneManagement;

// 前例どおり幹沿いconvexコライダーを作る
// Builds a trunk-hugging convex MeshCollider, matching the existing Tree.prefab precedent
public static class WrapperRayTargetBuilder
{
    private const string RayTargetObjectName = "RayTargetCollider";

    // 根元太さ測定高さ(最近接LODの割合)
    // Fraction of the nearest LOD's height used to measure how thick the object is at its base
    private const float FootprintHeightRatio = 0.1f;

    // 手書き前例のレイターゲットは最も細いTree.prefabでも見た目シルエット半径の13%(0.35m / 2.68m)あり、全前例がこの比率を満たす
    // Every hand-authored ray target keeps at least 13% of its own silhouette radius; Tree.prefab is the thinnest at 0.35m against 2.68m
    private const float MinimumVisualSilhouetteRatio = 0.13f;

    public static void Create(GameObject root, Bounds visualLocalBounds, List<Renderer> nearestLodRenderers)
    {
        var cylinderMesh = WrapperCylinderMesh.Resolve();

        // 樹冠まで含む外接ボックスにすると、採掘レンジ(2.5m)＋カメラ背後(3.5m)の内側にコライダー内壁が来て、レイが内側からは当たらず採掘不能になる
        // A canopy-wide box would put its inner wall within the mining reach (2.5m) plus the camera offset (3.5m), and Unity never hits a collider from inside
        var solidPoints = CollectSolidLocalPoints(root, nearestLodRenderers);
        var solidBoundsAccumulator = new WrapperLocalBoundsAccumulator(root.transform);
        foreach (var solidPoint in solidPoints) solidBoundsAccumulator.AddLocalPoint(solidPoint);
        if (!solidBoundsAccumulator.HasPoint) throw new InvalidOperationException($"no solid geometry to build a ray target for {root.name}");

        // 円柱はBK自前コライダーを内側に包む必要がある。はみ出た側から狙うとそちらが先にレイを取り、採掘対象を解決できない
        // The cylinder has to swallow BK's own colliders; where they poke out they would take the ray first and the mining target would never resolve
        var solidBounds = solidBoundsAccumulator.GetBounds();
        var axis = new Vector2(solidBounds.center.x, solidBounds.center.z);
        var radius = 0f;
        foreach (var solidPoint in solidPoints) radius = Mathf.Max(radius, (new Vector2(solidPoint.x, solidPoint.z) - axis).magnitude);

        // 地際のテーパーだけで太さを決めると、幹を持たない小型種のレイターゲットが数cmまで痩せて実質狙えなくなる
        // Sizing from the ground-level taper alone shrinks a trunkless small species down to a few centimetres, far too thin to aim at
        radius = Mathf.Max(radius, CalculateVisualSilhouetteRadius(root.transform, nearestLodRenderers, axis) * MinimumVisualSilhouetteRatio);

        var top = Mathf.Max(visualLocalBounds.max.y, solidBounds.max.y);
        var bottom = Mathf.Min(visualLocalBounds.min.y, solidBounds.min.y);
        if (radius <= 0f || top <= bottom) throw new InvalidOperationException($"ray target has no extent for {root.name}");

        // 採掘のレイキャストはMapObjectレイヤーだけを見るので、当たり判定はこのレイヤーに置く
        // The mining raycast only sees the MapObject layer, so the hit box lives there
        var rayTarget = new GameObject(RayTargetObjectName) { layer = LayerConst.MapObjectLayer };
        SceneManager.MoveGameObjectToScene(rayTarget, root.scene);
        rayTarget.transform.SetParent(root.transform, false);

        // 内接円半径でスケールしないと、面の中央方位だけ要求半径に届かず、そこからBK自前コライダーがはみ出して先にレイを取る
        // Scaling by anything but the inscribed radius leaves the face midpoints short, and BK's own colliders poke out there to take the ray first
        var meshRadius = WrapperCylinderMesh.CalculateInscribedRadius(cylinderMesh);
        var scale = new Vector3(radius / meshRadius, (top - bottom) * 0.5f / cylinderMesh.bounds.extents.y, radius / meshRadius);
        var center = new Vector3(axis.x, (top + bottom) * 0.5f, axis.y);
        rayTarget.transform.localScale = scale;
        rayTarget.transform.localPosition = center - Vector3.Scale(scale, cylinderMesh.bounds.center);

        var meshCollider = rayTarget.AddComponent<MeshCollider>();
        meshCollider.sharedMesh = cylinderMesh;
        meshCollider.convex = true;
        meshCollider.isTrigger = true;

        rayTarget.AddComponent<MapObjectRayTarget>();
    }

    // 見た目のシルエット半径を軸まわりで測る。遠景LODの膨らんだ外接ではなく最近接LODの実頂点で測る
    // Measures the silhouette radius around the axis on the nearest LOD's real vertices, not on a far LOD's inflated bounds
    private static float CalculateVisualSilhouetteRadius(Transform root, List<Renderer> nearestLodRenderers, Vector2 axis)
    {
        var worldToLocal = root.worldToLocalMatrix;
        var radius = 0f;
        foreach (var renderer in nearestLodRenderers)
        {
            var meshFilter = renderer.GetComponent<MeshFilter>();
            if (meshFilter == null || meshFilter.sharedMesh == null) continue;

            var toRootLocal = worldToLocal * renderer.transform.localToWorldMatrix;
            foreach (var vertex in meshFilter.sharedMesh.vertices)
            {
                var localVertex = toRootLocal.MultiplyPoint3x4(vertex);
                radius = Mathf.Max(radius, (new Vector2(localVertex.x, localVertex.z) - axis).magnitude);
            }
        }

        return radius;
    }

    // 幹・岩など実体のある部分の点群を、ルートのローカル空間で集める
    // Collects the points that make up the solid part, the trunk or the rock, in the root's local space
    private static List<Vector3> CollectSolidLocalPoints(GameObject root, List<Renderer> nearestLodRenderers)
    {
        // 根元の高さは頂点を拾う最近接LODの外接で決める。遠景LODは実物より膨らんだ外接を持つことがある
        // The base height comes from the very LOD whose vertices are sampled, since a far LOD can carry bounds fatter than the real silhouette
        var lodBoundsAccumulator = new WrapperLocalBoundsAccumulator(root.transform);
        foreach (var renderer in nearestLodRenderers) lodBoundsAccumulator.AddWorldBounds(renderer.bounds);
        if (!lodBoundsAccumulator.HasPoint) throw new InvalidOperationException($"no renderer to measure the ray target of {root.name}");

        var lodBounds = lodBoundsAccumulator.GetBounds();
        var footprintTop = lodBounds.min.y + lodBounds.size.y * FootprintHeightRatio;
        var worldToLocal = root.transform.worldToLocalMatrix;
        var solidPoints = new List<Vector3>();

        foreach (var collider in root.GetComponentsInChildren<Collider>(true)) AddColliderPoints(collider);

        // コライダーを持たない小石でも太さが決まるよう、根元付近の見た目からも点を取る
        // Sampling the near-ground silhouette too gives a thickness even to pebbles that carry no collider
        foreach (var renderer in nearestLodRenderers)
        {
            var meshFilter = renderer.GetComponent<MeshFilter>();
            if (meshFilter == null || meshFilter.sharedMesh == null) continue;
            AddMeshPoints(meshFilter.sharedMesh, renderer.transform, footprintTop);
        }

        return solidPoints;

        #region Internal

        void AddColliderPoints(Collider collider)
        {
            // メッシュコライダーは実形状で測る。外接ボックスの角まで円に含めると岩ひとつが実物よりかなり太くなる
            // A mesh collider is measured on its real shape; taking its AABB corners would make a single rock far fatter than it is
            var meshCollider = collider as MeshCollider;
            if (meshCollider != null && meshCollider.sharedMesh != null)
            {
                AddMeshPoints(meshCollider.sharedMesh, collider.transform, float.PositiveInfinity);
                return;
            }

            var cornerAccumulator = new WrapperLocalBoundsAccumulator(root.transform);
            cornerAccumulator.AddWorldBounds(collider.bounds);
            var colliderBounds = cornerAccumulator.GetBounds();
            for (var cornerIndex = 0; cornerIndex < 8; cornerIndex++)
                solidPoints.Add(new Vector3(
                    (cornerIndex & 1) == 0 ? colliderBounds.min.x : colliderBounds.max.x,
                    (cornerIndex & 2) == 0 ? colliderBounds.min.y : colliderBounds.max.y,
                    (cornerIndex & 4) == 0 ? colliderBounds.min.z : colliderBounds.max.z));
        }

        void AddMeshPoints(Mesh mesh, Transform meshTransform, float localHeightLimit)
        {
            var toRootLocal = worldToLocal * meshTransform.localToWorldMatrix;
            foreach (var vertex in mesh.vertices)
            {
                var localVertex = toRootLocal.MultiplyPoint3x4(vertex);
                if (localVertex.y <= localHeightLimit) solidPoints.Add(localVertex);
            }
        }

        #endregion
    }
}
