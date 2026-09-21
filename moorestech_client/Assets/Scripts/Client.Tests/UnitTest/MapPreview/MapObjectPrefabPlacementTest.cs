using System;
using Client.Game.InGame.Map.MapObject;
using Client.MapScene.Editor;
using Game.Map.Interface.Json;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Client.Tests.UnitTest.MapPreview
{
    public class MapObjectPrefabPlacementTest
    {
        private string _assetFolder;
        private GameObject _parent;

        [SetUp]
        public void SetUp()
        {
            _assetFolder = $"Assets/MapObjectPrefabPlacementTest_{Guid.NewGuid():N}";
            AssetDatabase.CreateFolder("Assets", _assetFolder.Substring("Assets/".Length));
            _parent = new GameObject("PlacementParent");
            _parent.transform.SetPositionAndRotation(new Vector3(12f, -6f, 8f), Quaternion.Euler(15f, 40f, -20f));
            _parent.transform.localScale = new Vector3(2f, 3f, 4f);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_parent);
            AssetDatabase.DeleteAsset(_assetFolder);
        }

        [Test]
        public void RestoresWorldPoseLocalScaleIdentityAndPrefabLinkUnderTransformedParent()
        {
            var prefab = CreatePrefab(true);
            var info = CreateInfo();
            var instance = MapObjectPrefabPlacement.Instantiate(info, prefab, _parent.transform);

            // 親の姿勢を受けても、世界座標とローカル倍率の意味を混ぜない
            // A transformed parent must not mix world pose with local scale
            Assert.That(Vector3.Distance(instance.transform.position, info.Position), Is.LessThan(0.0001f));
            Assert.That(Quaternion.Angle(instance.transform.rotation, info.Rotation), Is.LessThan(0.001f));
            Assert.That(instance.transform.localScale, Is.EqualTo(info.Scale));
            Assert.That(instance.transform.parent, Is.SameAs(_parent.transform));
            Assert.That(PrefabUtility.GetCorrespondingObjectFromSource(instance), Is.SameAs(prefab));

            var component = instance.GetComponent<MapObjectGameObject>();
            Assert.That(component.InstanceId, Is.EqualTo(info.InstanceId));
            Assert.That(component.MapObjectGuid, Is.EqualTo(info.MapObjectGuid));
            Assert.That(new SerializedObject(component).FindProperty("mapObjectGuid").stringValue, Is.EqualTo(info.MapObjectGuidStr));
        }

        [Test]
        public void LogsIdentityAndDestroysInstanceWhenComponentExistsOnlyOnChild()
        {
            var prefab = CreatePrefab(false);
            var info = CreateInfo();
            LogAssert.Expect(LogType.Error, $"Map object prefab root missing: InstanceId:{info.InstanceId} MapObjectGuid:{info.MapObjectGuidStr}");

            var instance = MapObjectPrefabPlacement.Instantiate(info, prefab, _parent.transform);

            Assert.That(instance, Is.Null);
            Assert.That(_parent.transform.childCount, Is.Zero);
            Assert.That(AssetDatabase.Contains(prefab), Is.True, "Rejecting an instance must preserve the borrowed prefab.");
        }

        private GameObject CreatePrefab(bool componentOnRoot)
        {
            // fixture資産だけを作成し、元のプロジェクト資産には触れない
            // Create only fixture assets and leave project assets untouched
            var source = new GameObject("PlacementFixture");
            try
            {
                var componentRoot = source;
                if (!componentOnRoot)
                {
                    componentRoot = new GameObject("Child");
                    componentRoot.transform.SetParent(source.transform);
                }
                componentRoot.AddComponent<MapObjectGameObject>();
                return PrefabUtility.SaveAsPrefabAsset(source, $"{_assetFolder}/Placement.prefab");
            }
            finally
            {
                Object.DestroyImmediate(source);
            }
        }

        private static MapObjectInfoJson CreateInfo()
        {
            var rotation = Quaternion.Euler(-32f, 71f, 13f);
            return new MapObjectInfoJson
            {
                InstanceId = 42, MapObjectGuidStr = "d92f0489-7b0c-4db6-a028-ad497dc1e7bc",
                X = -7f, Y = 2.5f, Z = 19f,
                RotationX = rotation.x, RotationY = rotation.y, RotationZ = rotation.z, RotationW = rotation.w,
                ScaleX = 0.4f, ScaleY = 1.7f, ScaleZ = 2.3f,
            };
        }
    }
}
