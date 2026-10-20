using System.Collections.Generic;
using Client.Game.InGame.Block;
using Client.Game.InGame.GraphicsQuality;
using Client.Game.InGame.GraphicsQuality.Appliers;
using Client.Game.InGame.Train.View.Object.Core;
using Game.Train.Unit;
using NUnit.Framework;
using UniRx;
using UnityEngine;

namespace Client.Tests.GraphicsQuality
{
    public class EffectsQualityEntryTest : GraphicsQualityTestScope
    {
        [Test]
        public void StartAndCreationAndPlacementEventsApplyToBlocksAndCarsAndReleaseDestroyedSwitches()
        {
            var store = Own(new GameObject("blocks")).AddComponent<BlockGameObjectDataStore>();
            var trains = Own(new GameObject("trains")).AddComponent<TrainCarObjectDatastore>();
            var existingBlock = CreateBlock("existing block");
            var existingCar = CreateCar("existing car");
            ((Dictionary<Vector3Int, BlockGameObject>)GetField(store, "_blockObjectsDictionary")).Add(Vector3Int.zero, existingBlock);
            ((Dictionary<TrainCarInstanceId, TrainCarEntityObject>)GetField(trains, "_entities")).Add(new TrainCarInstanceId(1), existingCar);
            var applier = new EffectsQualityApplier(store, trains);
            GraphicsQualityPreference.ApplyPreset(GraphicsQualityPreset.Low);
            applier.Start();
            Assert.IsFalse(existingBlock.GetComponent<Light>().enabled);
            Assert.IsFalse(existingCar.GetComponent<Light>().enabled);

            // 生成通知を通し新設個体と設置完了後の再停止を検証する
            // Verify new instances and stopping after placement through creation notifications
            var placed = CreateBlock("placed block");
            var newCar = CreateCar("new car");
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

            // 設定変更を挟まず実破棄だけで保持参照が外れる
            // Actual destruction releases retained references without another setting change
            var switches = (HashSet<EffectsQualitySwitch>)GetField(applier, "_switches");
            Assert.AreEqual(4, switches.Count);
            Object.DestroyImmediate(placed.gameObject);
            Assert.AreEqual(3, switches.Count);

            #region Internal

            BlockGameObject CreateBlock(string name)
            {
                var root = Own(new GameObject(name));
                root.AddComponent<Light>();
                return root.AddComponent<BlockGameObject>();
            }

            TrainCarEntityObject CreateCar(string name)
            {
                var root = Own(new GameObject(name));
                root.AddComponent<Light>();
                return root.AddComponent<TrainCarEntityObject>();
            }

            #endregion
        }
    }
}
