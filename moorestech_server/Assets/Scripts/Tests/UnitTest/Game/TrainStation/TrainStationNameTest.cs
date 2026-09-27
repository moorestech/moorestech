using System.Collections.Generic;
using Game.Block.Blocks.TrainRail;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using MessagePack;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Tests.Module.TestMod;
using Tests.Util;
using UniRx;
using UnityEngine;

namespace Tests.UnitTest.Game
{
    public class TrainStationNameTest
    {
        [Test]
        public void SetStationNameNotifiesAndExposesStateDetail()
        {
            var env = TrainTestHelper.CreateEnvironment();
            var block = TrainTestHelper.PlaceBlock(env, ForUnitTestModBlockId.TestTrainStation, Vector3Int.zero, BlockDirection.North);
            var station = block.GetComponent<TrainStationComponent>();
            Assert.AreEqual(string.Empty, station.StationName, "新設駅の初期名は空");

            // 名前変更はコンポーネントとブロックの両方から通知する
            // A name change must notify both component and block subscribers
            var componentNotifications = 0;
            var blockNotifications = 0;
            using (station.OnChangeBlockState.Subscribe(_ => componentNotifications++))
            using (block.BlockStateChange.Subscribe(_ => blockNotifications++))
            {
                station.SetStationName("北駅");
            }

            Assert.AreEqual("北駅", station.StationName);
            Assert.AreEqual(1, componentNotifications);
            Assert.AreEqual(1, blockNotifications);
            var detail = station.GetBlockStateDetails()[0];
            Assert.AreEqual(TrainStationNameStateDetail.BlockStateDetailKey, detail.Key);
            var decoded = MessagePackSerializer.Deserialize<TrainStationNameStateDetail>(detail.Value);
            Assert.AreEqual("北駅", decoded.StationName);
        }

        [Test]
        public void SavedStationNameRestoresWithoutChangingSaveFormat()
        {
            var original = new TrainStationComponent("test");
            var states = new Dictionary<string, object> { [original.SaveKey] = JToken.FromObject(original.GetSaveState()) };

            // 既存の文字列名を保存状態からそのまま復元する
            // Restore an existing string name directly from the saved component state
            var restored = new TrainStationComponent(states);

            Assert.AreEqual("test", restored.StationName);
            Assert.AreEqual("test", ((TrainStationComponent.TrainStationComponentSaveData)restored.GetSaveState()).stationName);
        }
    }
}
