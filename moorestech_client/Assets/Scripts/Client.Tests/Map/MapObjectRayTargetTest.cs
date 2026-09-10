using System.Collections.Generic;
using Client.Game.InGame.Interact;
using Client.Game.InGame.Interact.Selection;
using Client.Game.InGame.Map.MapObject;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Client.Tests.Map
{
    /// <summary>
    ///     レイターゲット円柱の太さとBK内包を検証
    ///     Verifies the ray target cylinder is thick enough to aim at and still swallows BK's own colliders
    /// </summary>
    // ラッパーはgitignoreされたPersonalAssetsのBKプレハブのバリアントで、CIには親が無く必ずnullになる
    // The wrappers are variants of BK prefabs under the gitignored PersonalAssets, whose parents never exist on CI
    [Category("IgnoreCI")]
    public class MapObjectRayTargetTest
    {
        // 届く距離は選定側が唯一の出所。カメラの背後距離だけはPlayerSystem.prefabのm_CameraDistance=3.5から写す
        // The reach comes solely from the selector; only the camera follow distance is copied from PlayerSystem.prefab m_CameraDistance = 3.5
        private const float CameraDistance = 3.5f;

        // 手書き前例のレイターゲットは最も細いTree.prefabでも見た目シルエット半径の13%(0.35m / 2.68m)あり、全前例がこの比率を満たす
        // Every hand-authored ray target keeps at least 13% of its own silhouette radius; Tree.prefab is the thinnest at 0.35m against 2.68m
        private const float MinimumVisualSilhouetteRatio = 0.13f;

        // 浮動小数の丸め分だけ許す
        // Allows for floating point rounding only
        private const float Tolerance = 1e-4f;

        private UnityEngine.SceneManagement.Scene _workScene;

        [SetUp]
        public void SetUp()
        {
            // カプセルコライダーの外接はシーンに居ないと引けないので、プレビューシーンへ実体化して測る
            // A capsule collider has no bounds outside a scene, so the measurement runs on an instance in a preview scene
            _workScene = EditorSceneManager.NewPreviewScene();
        }

        [TearDown]
        public void TearDown()
        {
            // アサート失敗でメソッド本体が途中終了しても、TearDownは必ず呼ばれるのでシーンは確実に閉じる
            // TearDown always runs even when the test body exits early on an assertion failure, so the scene is closed unconditionally
            EditorSceneManager.ClosePreviewScene(_workScene);
        }

        [Test]
        public void レイターゲットが採掘レンジから狙える太さでBK自前コライダーを包んでいる()
        {
            // カメラがコライダー内部に入るとUnityはそのコライダーへレイを当てないため、インタラクトで届く距離より太いレイターゲットは狙えなくなる
            // Unity never hits a collider from inside it, so a ray target wider than the camera can get is impossible to aim at
            var allowedReach = InteractTargetSelector.InteractDistance + CameraDistance;

            // 全species分の失敗を集めループ内でAssertを投げない（最初の1件で止まらず116件分を1回で見せる）
            // Failures are collected across every species instead of throwing inside the loop, so all 116 show up in one run instead of stopping at the first
            var failures = new List<string>();

            foreach (var element in MapObjectWrapperInventory.LoadSpecies())
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(element.wrapperPath);
                if (prefab == null)
                {
                    failures.Add($"wrapper prefab does not exist: {element.wrapperPath}");
                    continue;
                }

                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, _workScene);
                instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

                var rayTarget = LoadRayTargetCollider(instance, element.wrapperPath, failures);
                if (rayTarget != null)
                {
                    var axis = MapObjectRayTargetGeometry.CalculateAxis(instance, rayTarget);
                    var inscribedRadius = MapObjectRayTargetGeometry.CalculateInscribedRadius(instance, rayTarget, axis);
                    if (float.IsInfinity(inscribedRadius))
                    {
                        failures.Add($"ray target mesh has no side face, so the thickness assertions below would pass vacuously: {element.wrapperPath}");
                    }
                    else
                    {
                        // 実頂点で測る。外接ボックスの角で測ると多角柱の外へ√2だけ膨らみ、閾値までの余裕を実際より小さく見せる
                        // Measured on real vertices; AABB corners would inflate the prism by a factor of root two and understate the margin to the threshold
                        var reach = MapObjectRayTargetGeometry.CalculateCircumscribedReach(instance, rayTarget);
                        var bkReach = MapObjectRayTargetGeometry.CalculateBkColliderReach(instance, axis);

                        // BK自前コライダー自体が採掘レンジより広い巨岩（メサの丘・砂漠の大岩）は軸へ近づけず、必ず外側からレイが当たるので太さ上限を課さない
                        // A giant rock whose BK collider already exceeds the mining reach (mesa buttes, desert boulders) cannot be approached to its axis and is always hit from outside, so the width cap does not apply
                        if (allowedReach < reach && bkReach <= allowedReach)
                            failures.Add($"ray target is wider than the mining camera can stand off, so it can never be hit: {element.wrapperPath}");

                        // 地際のテーパーだけで太さを決めると幹を持たない小型種が数cmまで痩せるため、見た目に対する比率で下限を張る
                        // Sizing from the ground-level taper alone shrinks trunkless small species to a few centimetres, so the floor is a ratio of how big they look
                        var visualRadius = MapObjectRayTargetGeometry.CalculateVisualSilhouetteRadius(instance, axis);
                        if (inscribedRadius + Tolerance < visualRadius * MinimumVisualSilhouetteRatio)
                            failures.Add($"ray target is too thin against its own silhouette to aim at: {element.wrapperPath}");

                        // 多角柱は面の中央方位が最も細い。そこからBK自前コライダーがはみ出すと、その方位のレイをBK側が先に取り採掘対象を解決できない
                        // A prism is thinnest at its face midpoints; a BK collider poking out there takes the ray first and the mining target never resolves
                        if (inscribedRadius + Tolerance < bkReach)
                            failures.Add($"a BK collider pokes out of the ray target cylinder: {element.wrapperPath}");
                    }
                }

                Object.DestroyImmediate(instance);
            }

            Assert.IsEmpty(failures, $"{failures.Count} species failed:\n" + string.Join("\n", failures));
        }

        private static MeshCollider LoadRayTargetCollider(GameObject root, string wrapperPath, List<string> failures)
        {
            var rayTarget = root.GetComponentInChildren<MapObjectRayTarget>(true);
            if (rayTarget == null)
            {
                failures.Add($"no MapObjectRayTarget for the mining raycast: {wrapperPath}");
                return null;
            }

            // 前例(AddressableResources/Environment/Tree.prefab)と同じく幹に沿ったconvex MeshColliderであること
            // Same shape as the precedent (AddressableResources/Environment/Tree.prefab): a convex MeshCollider hugging the trunk
            var meshCollider = rayTarget.GetComponent<Collider>() as MeshCollider;
            if (meshCollider == null)
            {
                failures.Add($"ray target collider is not a MeshCollider: {wrapperPath}");
                return null;
            }

            if (!meshCollider.convex) failures.Add($"ray target MeshCollider is not convex: {wrapperPath}");
            if (!meshCollider.isTrigger) failures.Add($"ray target MeshCollider is not a trigger: {wrapperPath}");
            return meshCollider;
        }
    }
}
