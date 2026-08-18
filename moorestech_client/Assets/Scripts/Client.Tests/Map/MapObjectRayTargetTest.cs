using Client.Game.InGame.Map.MapObject;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Client.Tests.Map
{
    /// <summary>
    ///     ラッパーのレイターゲット円柱が、採掘レンジから狙える太さで、かつBK自前コライダーを内側に包んでいることを検証する
    ///     Verifies the wrapper's ray target cylinder is thick enough to aim at from the mining range and still swallows BK's own colliders
    /// </summary>
    public class MapObjectRayTargetTest
    {
        // 採掘可能距離(MiningController.miningDistance=2.5)とカメラの背後距離(PlayerSystem.prefabのm_CameraDistance=3.5)
        // The mining reach (MiningController.miningDistance = 2.5) and the camera's follow distance (PlayerSystem.prefab m_CameraDistance = 3.5)
        private const float MiningDistance = 2.5f;
        private const float CameraDistance = 3.5f;

        // 手書き前例のレイターゲットは最も細いTree.prefabでも見た目シルエット半径の13%(0.35m / 2.68m)あり、全前例がこの比率を満たす
        // Every hand-authored ray target keeps at least 13% of its own silhouette radius; Tree.prefab is the thinnest at 0.35m against 2.68m
        private const float MinimumVisualSilhouetteRatio = 0.13f;

        // 浮動小数の丸め分だけ許す
        // Allows for floating point rounding only
        private const float Tolerance = 1e-4f;

        [Test]
        public void レイターゲットが採掘レンジから狙える太さでBK自前コライダーを包んでいる()
        {
            // カメラがコライダー内部に入るとUnityはそのコライダーへレイを当てないため、採掘レンジで届く距離より太いレイターゲットは採掘不能になる
            // Unity never hits a collider from inside it, so a ray target wider than the camera can get is impossible to mine
            var allowedReach = MiningDistance + CameraDistance;

            // カプセルコライダーの外接はシーンに居ないと引けないので、プレビューシーンへ実体化して測る
            // A capsule collider has no bounds outside a scene, so the measurement runs on an instance in a preview scene
            var workScene = EditorSceneManager.NewPreviewScene();

            foreach (var element in MapObjectWrapperInventory.LoadSpecies())
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(element.wrapperPath);
                Assert.IsNotNull(prefab, $"wrapper prefab does not exist: {element.wrapperPath}");

                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, workScene);
                instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

                var rayTarget = LoadRayTargetCollider(instance, element.wrapperPath);
                var axis = MapObjectRayTargetGeometry.CalculateAxis(instance, rayTarget);
                var inscribedRadius = MapObjectRayTargetGeometry.CalculateInscribedRadius(instance, rayTarget, axis);
                Assert.IsFalse(float.IsInfinity(inscribedRadius), $"ray target mesh has no side face, so the thickness assertions below would pass vacuously: {element.wrapperPath}");

                // 実頂点で測る。外接ボックスの角で測ると多角柱の外へ√2だけ膨らみ、閾値までの余裕を実際より小さく見せる
                // Measured on real vertices; AABB corners would inflate the prism by a factor of root two and understate the margin to the threshold
                var reach = MapObjectRayTargetGeometry.CalculateCircumscribedReach(instance, rayTarget);
                Assert.LessOrEqual(reach, allowedReach, $"ray target is wider than the mining camera can stand off, so it can never be hit: {element.wrapperPath}");

                // 地際のテーパーだけで太さを決めると幹を持たない小型種が数cmまで痩せるため、見た目に対する比率で下限を張る
                // Sizing from the ground-level taper alone shrinks trunkless small species to a few centimetres, so the floor is a ratio of how big they look
                var visualRadius = MapObjectRayTargetGeometry.CalculateVisualSilhouetteRadius(instance, axis);
                Assert.GreaterOrEqual(inscribedRadius + Tolerance, visualRadius * MinimumVisualSilhouetteRatio, $"ray target is too thin against its own silhouette to aim at: {element.wrapperPath}");

                // 多角柱は面の中央方位が最も細い。そこからBK自前コライダーがはみ出すと、その方位のレイをBK側が先に取り採掘対象を解決できない
                // A prism is thinnest at its face midpoints; a BK collider poking out there takes the ray first and the mining target never resolves
                var bkReach = MapObjectRayTargetGeometry.CalculateBkColliderReach(instance, axis);
                Assert.LessOrEqual(bkReach, inscribedRadius + Tolerance, $"a BK collider pokes out of the ray target cylinder: {element.wrapperPath}");

                Object.DestroyImmediate(instance);
            }

            EditorSceneManager.ClosePreviewScene(workScene);
        }

        private static MeshCollider LoadRayTargetCollider(GameObject root, string wrapperPath)
        {
            var rayTarget = root.GetComponentInChildren<MapObjectRayTarget>(true);
            Assert.IsNotNull(rayTarget, $"no MapObjectRayTarget for the mining raycast: {wrapperPath}");

            // 前例(AddressableResources/Environment/Tree.prefab)と同じく幹に沿ったconvex MeshColliderであること
            // Same shape as the precedent (AddressableResources/Environment/Tree.prefab): a convex MeshCollider hugging the trunk
            var meshCollider = rayTarget.GetComponent<Collider>() as MeshCollider;
            Assert.IsNotNull(meshCollider, $"ray target collider is not a MeshCollider: {wrapperPath}");
            Assert.IsTrue(meshCollider.convex, $"ray target MeshCollider is not convex: {wrapperPath}");
            Assert.IsTrue(meshCollider.isTrigger, $"ray target MeshCollider is not a trigger: {wrapperPath}");
            return meshCollider;
        }
    }
}
