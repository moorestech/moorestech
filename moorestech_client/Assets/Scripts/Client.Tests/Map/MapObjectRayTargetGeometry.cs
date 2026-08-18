using System.Collections.Generic;
using Client.Common;
using Client.Game.InGame.Map.MapObject;
using UnityEngine;

namespace Client.Tests.Map
{
    /// <summary>
    ///     レイターゲット円柱・シルエット・BK寸法を測る
    ///     Measures the ray target cylinder, its silhouette, and BK's own colliders, all in the root's local space
    /// </summary>
    public static class MapObjectRayTargetGeometry
    {
        // 蓋の三角形と縮退三角形を落とす閾値。側面の法線は桁違いに大きい
        // Threshold that drops cap and degenerate triangles; a side face normal is orders of magnitude larger
        private const float SideFaceNormalThreshold = 1e-12f;

        public static Vector2 CalculateAxis(GameObject root, MeshCollider rayTarget)
        {
            var center = ToRootLocal(root, rayTarget).MultiplyPoint3x4(rayTarget.sharedMesh.bounds.center);
            return new Vector2(center.x, center.z);
        }

        // ルートの原点(=プレイヤーが近づく位置)から円柱の外周までの水平距離。実頂点で測るので外接ボックスの√2膨張が乗らない
        // Horizontal distance from the root origin, where the player walks up to, out to the cylinder's far side, measured on real vertices so no AABB root-two inflation creeps in
        public static float CalculateCircumscribedReach(GameObject root, MeshCollider rayTarget)
        {
            var toRootLocal = ToRootLocal(root, rayTarget);
            var reach = 0f;
            foreach (var vertex in rayTarget.sharedMesh.vertices)
            {
                var localVertex = toRootLocal.MultiplyPoint3x4(vertex);
                reach = Mathf.Max(reach, new Vector2(localVertex.x, localVertex.z).magnitude);
            }

            return reach;
        }

        // 円柱の側面が軸へ最も近づく距離。多角柱はここが最も細く、方位によらず包める範囲はこれで決まる
        // How close the cylinder's side wall comes to its axis; a prism is thinnest there, so this is what it covers no matter the azimuth
        public static float CalculateInscribedRadius(GameObject root, MeshCollider rayTarget, Vector2 axis)
        {
            var toRootLocal = ToRootLocal(root, rayTarget);
            var vertices = rayTarget.sharedMesh.vertices;
            var triangles = rayTarget.sharedMesh.triangles;
            var inscribedRadius = float.PositiveInfinity;

            for (var index = 0; index < triangles.Length; index += 3)
            {
                var first = toRootLocal.MultiplyPoint3x4(vertices[triangles[index]]);
                var second = toRootLocal.MultiplyPoint3x4(vertices[triangles[index + 1]]);
                var third = toRootLocal.MultiplyPoint3x4(vertices[triangles[index + 2]]);

                // 半径を決めるのは側面だけで、上下の蓋は水平方向の法線成分を持たない
                // Only the side faces define the radius; the top and bottom caps carry no horizontal normal component
                var normal = Vector3.Cross(second - first, third - first);
                var horizontalNormal = new Vector2(normal.x, normal.z);
                if (horizontalNormal.sqrMagnitude < SideFaceNormalThreshold) continue;
                inscribedRadius = Mathf.Min(inscribedRadius, Mathf.Abs(Vector2.Dot(horizontalNormal.normalized, new Vector2(first.x, first.z) - axis)));
            }

            return inscribedRadius;
        }

        // 見た目のシルエット半径。遠景LODの膨らんだ外接ではなく最近接LODの実頂点で測る
        // The silhouette radius, measured on the nearest LOD's real vertices rather than a far LOD's inflated bounds
        public static float CalculateVisualSilhouetteRadius(GameObject root, Vector2 axis)
        {
            var radius = 0f;
            foreach (var renderer in CollectNearestLodRenderers(root))
            {
                // アウトラインは最近接LODの複製なので、測っても半径は変わらない
                // The outline duplicates the nearest LOD, so measuring it would change nothing
                if (renderer.gameObject.layer == LayerConst.OutlineLayer) continue;

                var meshFilter = renderer.GetComponent<MeshFilter>();
                if (meshFilter == null || meshFilter.sharedMesh == null) continue;

                var toRootLocal = ToRootLocal(root, renderer);
                foreach (var vertex in meshFilter.sharedMesh.vertices)
                {
                    var localVertex = toRootLocal.MultiplyPoint3x4(vertex);
                    radius = Mathf.Max(radius, (new Vector2(localVertex.x, localVertex.z) - axis).magnitude);
                }
            }

            return radius;
        }

        // BK自前コライダーが軸から最も遠ざかる距離。生成器と同じく、メッシュは実形状で、それ以外は外接ボックスの角で測る
        // How far BK's own colliders reach from the axis, measured like the generator does: real shape for meshes, AABB corners otherwise
        public static float CalculateBkColliderReach(GameObject root, Vector2 axis)
        {
            var worldToLocal = root.transform.worldToLocalMatrix;
            var reach = 0f;

            foreach (var collider in root.GetComponentsInChildren<Collider>(true))
            {
                if (collider.GetComponent<MapObjectRayTarget>() != null) continue;

                var meshCollider = collider as MeshCollider;
                if (meshCollider != null && meshCollider.sharedMesh != null)
                {
                    var toRootLocal = ToRootLocal(root, collider);
                    foreach (var vertex in meshCollider.sharedMesh.vertices)
                    {
                        var localVertex = toRootLocal.MultiplyPoint3x4(vertex);
                        reach = Mathf.Max(reach, (new Vector2(localVertex.x, localVertex.z) - axis).magnitude);
                    }

                    continue;
                }

                var colliderBounds = collider.bounds;
                for (var cornerIndex = 0; cornerIndex < 8; cornerIndex++)
                {
                    var corner = new Vector3(
                        (cornerIndex & 1) == 0 ? colliderBounds.min.x : colliderBounds.max.x,
                        (cornerIndex & 2) == 0 ? colliderBounds.min.y : colliderBounds.max.y,
                        (cornerIndex & 4) == 0 ? colliderBounds.min.z : colliderBounds.max.z);
                    var localCorner = worldToLocal.MultiplyPoint3x4(corner);
                    reach = Mathf.Max(reach, (new Vector2(localCorner.x, localCorner.z) - axis).magnitude);
                }
            }

            return reach;
        }

        private static Matrix4x4 ToRootLocal(GameObject root, Component child)
        {
            return root.transform.worldToLocalMatrix * child.transform.localToWorldMatrix;
        }

        private static List<Renderer> CollectNearestLodRenderers(GameObject root)
        {
            var renderers = new List<Renderer>();
            var lodGroup = root.GetComponentInChildren<LODGroup>(true);
            if (lodGroup == null)
            {
                renderers.AddRange(root.GetComponentsInChildren<MeshRenderer>(true));
                return renderers;
            }

            foreach (var renderer in lodGroup.GetLODs()[0].renderers)
                if (renderer != null)
                    renderers.Add(renderer);
            return renderers;
        }
    }
}
