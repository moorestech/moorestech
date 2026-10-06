using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.Map.Surface
{
    internal static class SurfaceTerrainIsolation
    {
        internal static Vector3 ResolveTranslation(Vector2 originalOrigin)
        {
            float rightEdge = 0f;

            // 既存地形を変更せず全既存矩形の右側へテスト地形を隔離する
            // Isolate test terrain to the right of every existing rectangle without modifying existing terrains
            foreach (var terrain in Terrain.activeTerrains)
            {
                float edge = terrain.transform.position.x + terrain.terrainData.size.x;
                Assert.That(float.IsNaN(edge) || float.IsInfinity(edge), Is.False, "Existing terrain bounds must be finite");
                rightEdge = Mathf.Max(rightEdge, edge);
            }
            return new Vector3(rightEdge + 32f - originalOrigin.x, 0f, 0f);
        }

    }
}
