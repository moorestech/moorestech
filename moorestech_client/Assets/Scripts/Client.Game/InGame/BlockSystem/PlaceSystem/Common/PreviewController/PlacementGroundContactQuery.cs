using Client.Common;
using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Common.PreviewController
{
    internal static class PlacementGroundContactQuery
    {
        private static Collider[] _candidates = new Collider[8];

        internal static bool HasOverlap(GroundCollisionDetector[] detectors, Scene scene)
        {
            var physics = scene.GetPhysicsScene();
            foreach (var detector in detectors)
            {
                if (!detector.isActiveAndEnabled) continue;
                foreach (var source in detector.GetComponentsInChildren<Collider>(true))
                {
                    if (!source.enabled || !source.gameObject.activeInHierarchy) continue;
                    if (HasOverlap(source, physics)) return true;
                }
            }
            return false;
        }

        private static bool HasOverlap(Collider source, PhysicsScene physics)
        {
            // 現配置の広域候補を取得し、実形状の侵入判定で絞る
            // Find broad-phase candidates at the current pose, then test actual shapes.
            var bounds = source.bounds;
            int count;
            while (true)
            {
                count = physics.OverlapBox(bounds.center, bounds.extents, _candidates,
                    Quaternion.identity, 1 << LayerConst.GroundLayer, QueryTriggerInteraction.Collide);
                if (count < _candidates.Length) break;
                System.Array.Resize(ref _candidates, _candidates.Length * 2);
            }

            for (var index = 0; index < count; index++)
            {
                var ground = _candidates[index];
                if (!ground.gameObject.TryGetComponent<GroundGameObject>(out _)) continue;
                if (!SupportsPenetration(source) && !SupportsPenetration(ground))
                {
                    // 精密判定不能なら許可側へ倒さず、理由を開発ログへ残す
                    // If exact testing is unsupported, reject placement and log why.
                    Debug.LogError($"Terrain contact query conservatively blocks non-convex pair: {source.name}, {ground.name}");
                    return true;
                }
                if (Physics.ComputePenetration(source, source.transform.position, source.transform.rotation,
                        ground, ground.transform.position, ground.transform.rotation, out _, out _)) return true;
            }
            return false;
        }

        private static bool SupportsPenetration(Collider collider)
        {
            return collider is BoxCollider || collider is SphereCollider || collider is CapsuleCollider ||
                   collider is MeshCollider mesh && mesh.convex;
        }
    }
}
