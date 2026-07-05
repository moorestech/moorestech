using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.StateProcessor;
using Game.Gear.Common;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Client.Tests.Gear
{
    public class GearPrefabFourDirectionConsistencyTest
    {
        private static readonly string[] GearShaftPrefabPaths =
        {
            "Assets/AddressableResources/Block/Shaft.prefab",
            "Assets/AddressableResources/Block/Shaft Iron.prefab",
            "Assets/AddressableResources/Block/Shaft Vertical.prefab",
            "Assets/AddressableResources/Block/Shaft Vertical Iron.prefab",
            "Assets/AddressableResources/Block/GearBeltConveyor Shaft.prefab",
            "Assets/AddressableResources/Block/BigGear.prefab",
            "Assets/AddressableResources/Block/SmallGear.prefab",
            "Assets/AddressableResources/Block/GearChainPole.prefab",
            "Assets/AddressableResources/Block/CompactGearChainPole.prefab",
        };

        [Test]
        public void 対向設置で同一ワールド方向に回転する([ValueSource(nameof(GearShaftPrefabPaths))] string prefabPath)
        {
            // 180度差の設置ペアで全回転パーツのワールド回転方向が一致することを検証
            // Verify all rotating parts spin the same world direction for placements differing by 180 degrees
            AssertSameWorldSpin(prefabPath, 0f, 180f);
            AssertSameWorldSpin(prefabPath, 90f, 270f);
        }

        private static void AssertSameWorldSpin(string prefabPath, float yawA, float yawB)
        {
            var deltasA = CollectSpinDeltas(prefabPath, yawA);
            var deltasB = CollectSpinDeltas(prefabPath, yawB);
            Assert.AreEqual(deltasA.Count, deltasB.Count);

            for (var i = 0; i < deltasA.Count; i++)
            {
                deltasA[i].ToAngleAxis(out var angleA, out var axisA);
                deltasB[i].ToAngleAxis(out var angleB, out var axisB);

                // 回転しないパーツ(rotationSpeed=0等)はスキップ
                // Skip parts that do not rotate (e.g. rotationSpeed = 0)
                if (angleA < 0.1f && angleB < 0.1f) continue;

                var label = $"{prefabPath} yaw {yawA} vs {yawB} part {i}";
                Assert.AreEqual(angleA, angleB, 0.01f, label);
                Assert.Greater(Vector3.Dot(axisA, axisB), 0.9f, label);
            }
        }

        private static List<Quaternion> CollectSpinDeltas(string prefabPath, float yaw)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            Assert.IsNotNull(prefab, prefabPath);
            var instance = Object.Instantiate(prefab, Vector3.zero, Quaternion.Euler(0, yaw, 0));
            var processor = instance.GetComponentInChildren<GearStateChangeProcessor>(true);
            Assert.IsNotNull(processor, prefabPath);

            // 回転前後のワールド回転を記録して差分を取る
            // Record world rotations before and after rotating and take the delta
            var targets = new List<Transform>();
            foreach (var info in processor.RotationInfos)
            {
                if (info?.RotationTransform != null) targets.Add(info.RotationTransform);
            }

            var before = new List<Quaternion>();
            foreach (var target in targets) before.Add(target.rotation);

            processor.Rotate(new GearStateDetail(true, 60f, 0f), 1f / 60f);

            var deltas = new List<Quaternion>();
            for (var i = 0; i < targets.Count; i++) deltas.Add(targets[i].rotation * Quaternion.Inverse(before[i]));

            Object.DestroyImmediate(instance);
            return deltas;
        }
    }
}
