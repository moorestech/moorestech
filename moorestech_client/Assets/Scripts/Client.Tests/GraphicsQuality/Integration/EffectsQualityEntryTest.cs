using System.Collections;
using System.Collections.Generic;
using Client.Game.InGame.Block;
using Client.Game.InGame.GraphicsQuality;
using Client.Game.InGame.GraphicsQuality.Appliers;
using Client.Game.InGame.Train.View.Object.Core;
using Game.Train.Unit;
using NUnit.Framework;
using UniRx;
using UnityEngine;
using UnityEngine.TestTools;

namespace Client.Tests.GraphicsQuality
{
    public class EffectsQualityEntryTest : GraphicsQualityTestScope
    {
        [UnityTest]
        public IEnumerator StartAndCreationAndPlacementEventsApplyToBlocksAndCarsAndReleaseDestroyedSwitches()
        {
            var store = Own(new GameObject("blocks")).AddComponent<BlockGameObjectDataStore>();
            var trains = Own(new GameObject("trains")).AddComponent<TrainCarObjectDatastore>();
            var existingBlock = CreateBlock("existing block", null);
            var existingCar = CreateCar("existing car", null);
            ((Dictionary<Vector3Int, BlockGameObject>)GetField(store, "_blockObjectsDictionary")).Add(Vector3Int.zero, existingBlock);
            ((Dictionary<TrainCarInstanceId, TrainCarEntityObject>)GetField(trains, "_entities")).Add(new TrainCarInstanceId(1), existingCar);
            var applier = new EffectsQualityApplier(store, trains);
            GraphicsQualityPreference.ApplyPreset(GraphicsQualityPreset.Low);
            applier.Start();
            Assert.IsFalse(existingBlock.GetComponent<Light>().enabled);
            Assert.IsFalse(existingCar.GetComponent<Light>().enabled);

            // 生成・設置完了通知から再停止を検証
            // Verify new instances and stopping again through creation and placement-completion notifications
            var placed = CreateBlock("placed block", null);
            var newCar = CreateCar("new car", null);
            ((Subject<BlockGameObject>)GetField(store, "_onBlockPlaced")).OnNext(placed);
            ((Subject<TrainCarEntityObject>)GetField(trains, "_onTrainCarCreated")).OnNext(newCar);
            Assert.IsFalse(placed.GetComponent<Light>().enabled);
            Assert.IsFalse(newCar.GetComponent<Light>().enabled);
            placed.GetComponent<Light>().enabled = true;
            ((Subject<BlockGameObject>)GetField(placed, "_onFinishedPlaceAnimation")).OnNext(placed);
            Assert.IsFalse(placed.GetComponent<Light>().enabled);
            GraphicsQualityPreference.ApplyPreset(GraphicsQualityPreset.High);
            foreach (var root in new[] { existingBlock.gameObject, existingCar.gameObject, placed.gameObject, newCar.gameObject })
                Assert.IsTrue(root.GetComponent<Light>().enabled);

            // 設定変更なしでも破棄で保持解除
            // Destruction releases references without a setting change
            var switches = (HashSet<EffectsQualitySwitch>)GetField(applier, "_switches");
            Assert.AreEqual(4, switches.Count);
            Object.DestroyImmediate(placed.gameObject);
            Assert.AreEqual(3, switches.Count);

            // 未有効化個体も数フレーム後に保持解除
            // Release never-activated instances after destruction and frame advancement
            var inactiveParent = Own(new GameObject("inactive parent"));
            inactiveParent.SetActive(false);
            var inactiveBlock = CreateBlock("inactive block", inactiveParent.transform);
            var inactiveCar = CreateCar("inactive car", inactiveParent.transform);
            ((Subject<BlockGameObject>)GetField(store, "_onBlockPlaced")).OnNext(inactiveBlock);
            ((Subject<TrainCarEntityObject>)GetField(trains, "_onTrainCarCreated")).OnNext(inactiveCar);
            Assert.AreEqual(5, switches.Count);
            Object.DestroyImmediate(inactiveBlock.gameObject);
            Object.DestroyImmediate(inactiveCar.gameObject);
            for (var frame = 0; frame < 10 && switches.Count != 3; frame++) yield return null;
            Assert.AreEqual(3, switches.Count);

            // 解除後の設定通知が残存個体にだけ届く
            // Setting changes after release reach only surviving instances
            GraphicsQualityPreference.ApplyPreset(GraphicsQualityPreset.Low);
            foreach (var root in new[] { existingBlock.gameObject, existingCar.gameObject, newCar.gameObject })
                Assert.IsFalse(root.GetComponent<Light>().enabled);

            #region Internal

            BlockGameObject CreateBlock(string name, Transform parent)
            {
                var root = Own(new GameObject(name));
                root.transform.SetParent(parent, false);
                root.AddComponent<Light>();
                return root.AddComponent<BlockGameObject>();
            }

            TrainCarEntityObject CreateCar(string name, Transform parent)
            {
                var root = Own(new GameObject(name));
                root.transform.SetParent(parent, false);
                root.AddComponent<Light>();
                return root.AddComponent<TrainCarEntityObject>();
            }

            #endregion
        }
    }
}
