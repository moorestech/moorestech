using System;
using Game.MapGeneration.Surface;
using UnityEngine;

namespace Client.Game.InGame.Map.Outcrop
{
    public static class OutcropSurfacePlacement
    {
        // 露頭の底を地表のZファイトから浮かせる量（採掘底面の余裕とは別の意味）
        // How far an outcrop base is lifted off the ground to avoid z-fighting (unrelated to the mining clearance)
        private const float OutcropGroundLiftMeters = 0.001f;

        public static void Place(GameObject instance, Bounds veinBounds, TerrainSurfacePresentation presentation)
        {
            switch (presentation)
            {
                case TerrainSurfacePresentation.Legacy:
                    return;
                case TerrainSurfacePresentation.Grounded grounded:
                    PlaceOnGround(instance, veinBounds, grounded.Envelope);
                    return;
                default:
                    throw Failure("[OutcropSurfacePlacement] Unknown surface presentation.");
            }
        }

        private static void PlaceOnGround(GameObject instance, Bounds veinBounds, SurfaceEnvelope envelope)
        {
            var renderers = instance.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
                throw Failure($"[OutcropSurfacePlacement] No renderer on {instance.name}.");

            // 作者のpivotを寸法と混同せず実体の全描画範囲を合成する
            // Combine instance bounds without confusing authored pivots with dimensions
            var meshBounds = new Bounds();
            var hasVisibleRenderer = false;
            foreach (var renderer in renderers)
            {
                if (!renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
                if (hasVisibleRenderer) meshBounds.Encapsulate(renderer.bounds);
                else meshBounds = renderer.bounds;
                hasVisibleRenderer = true;
            }
            if (!hasVisibleRenderer)
                throw Failure($"[OutcropSurfacePlacement] No active renderer on {instance.name}.");
            if (envelope.CoreHalfSize * 2f < meshBounds.size.x || envelope.CoreHalfSize * 2f < meshBounds.size.z)
                throw Failure($"[OutcropSurfacePlacement] {instance.name} exceeds the grading core: {meshBounds.size}.");

            var center = veinBounds.center;
            var terrain = FindContainingTerrain(center);
            var groundHeight = terrain.SampleHeight(center) + terrain.transform.position.y;

            // 全meshの中心と底を平坦coreへ
            // Align the whole mesh center and bottom with the flat core
            var shift = new Vector3(center.x - meshBounds.center.x,
                groundHeight + OutcropGroundLiftMeters - meshBounds.min.y, center.z - meshBounds.center.z);
            instance.transform.position += shift;
        }

        private static Terrain FindContainingTerrain(Vector3 center)
        {
            Terrain selected = null;
            foreach (var terrain in Terrain.activeTerrains)
            {
                var origin = terrain.transform.position;
                var size = terrain.terrainData.size;
                if (center.x < origin.x || origin.x + size.x < center.x ||
                    center.z < origin.z || origin.z + size.z < center.z) continue;

                // 共有境界は原点の辞書順で選ぶ
                // Select shared boundaries by lexicographic terrain origin
                if (selected == null || origin.x < selected.transform.position.x ||
                    (origin.x == selected.transform.position.x && origin.z < selected.transform.position.z))
                    selected = terrain;
            }
            if (selected == null)
                throw Failure($"[OutcropSurfacePlacement] No terrain contains {center}.");
            return selected;
        }
        private static InvalidOperationException Failure(string reason)
        {
            // 拒否理由を起動失敗の前に記録する
            // Record the rejection before failing startup
            Debug.LogError(reason);
            return new InvalidOperationException(reason);
        }
    }
}
