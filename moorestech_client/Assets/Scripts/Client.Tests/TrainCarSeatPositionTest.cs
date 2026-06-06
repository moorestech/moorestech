using Client.Game.InGame.Train.View.Object;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Client.Tests
{
    public class TrainCarSeatPositionTest
    {
        [Test]
        public void TryGetSeatPosition_ReturnsPrefabMarkerTransform_BySeatIndex()
        {
            // Prefab markerのindexで座席Transformを解決する
            // Resolve the seat Transform by the Prefab marker index
            var root = new GameObject("TrainCarRoot");
            try
            {
                root.AddComponent<Rigidbody>();
                var entity = root.AddComponent<TrainCarEntityObject>();
                var seatObject = new GameObject("SeatPosition1");
                seatObject.transform.SetParent(root.transform, false);
                var marker = seatObject.AddComponent<SeatPosition>();
                var serializedMarker = new SerializedObject(marker);
                serializedMarker.FindProperty("seatIndex").intValue = 1;
                serializedMarker.ApplyModifiedPropertiesWithoutUndo();

                // 初期化時にmarkerを一括取得して保持する
                // Cache all markers during entity initialization
                entity.Initialize();
                var resolved = entity.TryGetSeatPosition(1, out var seatTransform);
                var missing = entity.TryGetSeatPosition(0, out _);

                Assert.IsTrue(resolved);
                Assert.AreSame(seatObject.transform, seatTransform);
                Assert.IsFalse(missing);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void AddressableTrainPrefabs_DefineExpectedSeatPositionMarkers()
        {
            // Addressable Prefab側の座席marker配置を検証する
            // Verify seat marker placement on Addressable Prefabs
            AssertSeatMarker(
                "Assets/AddressableResources/Train/Locomotive.prefab",
                "Locomotive/VisualRoot/Engine/SeatPos",
                0);
            AssertSeatMarker(
                "Assets/AddressableResources/Train/CargoCar.prefab",
                "CargoCar/SeatPos_1",
                0);
            AssertSeatMarker(
                "Assets/AddressableResources/Train/CargoCar.prefab",
                "CargoCar/SeatPos_2",
                1);
        }

        private static void AssertSeatMarker(string prefabPath, string expectedPath, int expectedSeatIndex)
        {
            // Prefab assetからSeatPositionを取得してpathとindexを照合する
            // Read SeatPosition from the Prefab asset and compare path and index
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            Assert.IsNotNull(prefab, prefabPath);
            var markers = prefab.GetComponentsInChildren<SeatPosition>(true);
            for (var i = 0; i < markers.Length; i++)
            {
                if (markers[i].GetSeatIndex() != expectedSeatIndex)
                {
                    continue;
                }
                Assert.AreEqual(expectedPath, GetPath(markers[i].transform));
                return;
            }
            Assert.Fail($"SeatPosition not found. Prefab:{prefabPath} SeatIndex:{expectedSeatIndex}");
        }

        private static string GetPath(Transform target)
        {
            // Transform階層をrootからのpath文字列に変換する
            // Convert the Transform hierarchy to a path string from the root
            var path = target.name;
            while (target.parent != null)
            {
                target = target.parent;
                path = target.name + "/" + path;
            }
            return path;
        }
    }
}
