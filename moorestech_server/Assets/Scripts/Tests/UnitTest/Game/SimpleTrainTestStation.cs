using Core.Master;
using Game.Block.Blocks.TrainRail;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.Train.RailGraph;
using Game.Train.Utility;
using Mooresmaster.Model.BlocksModule;
using NUnit.Framework;
using System.Linq;
using Tests.Module.TestMod;
using Tests.Util;
using UnityEngine;

namespace Tests.UnitTest.Game
{
    public class SimpleTrainTestStation
    {
        /// <summary>
        /// RailComponentを2つ設置し、手動で接続。FindShortestPathで接続されていることを確認
        /// </summary>
        [Test]
        public void TestRailComponentsAreConnected()
        {
            var env = TrainTestHelper.CreateEnvironment();

            var railComponent1 = TrainTestHelper.PlaceRail(env, new Vector3Int(0, 0, 0), BlockDirection.North);
            var railComponent2 = TrainTestHelper.PlaceRail(env, new Vector3Int(1, 0, 0), BlockDirection.North);

            Assert.NotNull(railComponent1, "レールコンポーネント1の生成に失敗しています。");
            Assert.NotNull(railComponent2, "レールコンポーネント2の生成に失敗しています。");

            railComponent1.ConnectRailComponent(railComponent2, true, true);

            var connectedNodes = railComponent1.FrontNode.ConnectedNodesWithDistance;
            var connectedNode = connectedNodes.FirstOrDefault();

            Assert.NotNull(connectedNode, "RailComponent1のFrontNodeがRailComponent2と接続されていません。");
            Assert.AreEqual(railComponent2.FrontNode, connectedNode.Item1, "RailComponent1のFrontNodeがRailComponent2のFrontNodeと一致していません。");

            var path = RailGraphDatastore.FindShortestPath(railComponent1.FrontNode, railComponent2.FrontNode);
            Assert.AreNotEqual(0, path.Count, "接続後のFrontNode間で最短経路が見つかりませんでした。");

            path = RailGraphDatastore.FindShortestPath(railComponent2.BackNode, railComponent2.BackNode);
            Assert.AreNotEqual(0, path.Count, "BackNode同士での最短経路探索が失敗しました。");
        }

        /// <summary>
        /// 駅ブロックの向きごとのRailComponent位置を検証
        /// </summary>
        [Test]
        public void StationDirectionSimple()
        {
            var env = TrainTestHelper.CreateEnvironment();

            var (stationBlockA, railSaver) = TrainTestHelper.PlaceBlockWithComponent<RailSaverComponent>(
                env,
                ForUnitTestModBlockId.TestTrainStation,
                new Vector3Int(0, 0, 0),
                BlockDirection.North);
            Assert.IsNotNull(stationBlockA, "駅ブロックの設置に失敗しました。");
            Assert.IsNotNull(railSaver, "RailSaverComponentの取得に失敗しました。");

            Assert.AreEqual(2, railSaver.RailComponents.Length, "駅ブロックに紐づくRailComponent数が期待値と一致しません。");
            var railComponentA = railSaver.RailComponents[0];
            var railComponentB = railSaver.RailComponents[1];
            Debug.Log("railComponentA Position: " + railComponentA.Position);
            Debug.Log("railComponentB Position: " + railComponentB.Position);
        }

        /// <summary>
        /// 駅ブロックの向きごとのRailComponent位置を検証
        /// Debug.logで位置を出力し、目視で確認する
        /// </summary>
        [Test]
        public void StationDirectionMain()
        {
            var env = TrainTestHelper.CreateEnvironment();

            for (int i = 0; i < 4; i++)
            {
                var direction = (BlockDirection)4 + i;
                var (stationBlockA, railSaver) = TrainTestHelper.PlaceBlockWithComponent<RailSaverComponent>(
                    env,
                    ForUnitTestModBlockId.TestTrainStation,
                    new Vector3Int(0, 5 * i, 0),
                    direction);
                Assert.IsNotNull(stationBlockA, "駅ブロックの設置に失敗しました。");
                Assert.IsNotNull(railSaver, "RailSaverComponentの取得に失敗しました。");

                Assert.AreEqual(2, railSaver.RailComponents.Length, "駅ブロックに紐づくRailComponent数が期待値と一致しません。");
                var railComponentA = railSaver.RailComponents[0];
                var railComponentB = railSaver.RailComponents[1];
                Debug.Log("railComponentA Position: " + railComponentA.Position);
                Debug.Log("railComponentB Position: " + railComponentB.Position);
            }
        }

        [Test]
        public void StationConnectionSimple()
        {
            var env = TrainTestHelper.CreateEnvironment();

            var (_, firstSaver) = TrainTestHelper.PlaceBlockWithComponent<RailSaverComponent>(
                env,
                ForUnitTestModBlockId.TestTrainStation,
                new Vector3Int(0, 0, 0),
                BlockDirection.North);
            var railNodeA = firstSaver.RailComponents[0].FrontNode;
            var railNodeB = firstSaver.RailComponents[1].FrontNode;

            var stationParam = (TrainStationBlockParam)MasterHolder.BlockMaster
                .GetBlockMaster(ForUnitTestModBlockId.TestTrainStation).BlockParam;
            var stationPosition = new Vector3Int(0, 0, 8);

            var (_, secondSaver) = TrainTestHelper.PlaceBlockWithComponent<RailSaverComponent>(
                env,
                ForUnitTestModBlockId.TestTrainStation,
                stationPosition,
                BlockDirection.North);
            var railNodeC = secondSaver.RailComponents[0].FrontNode;
            var railNodeD = secondSaver.RailComponents[1].FrontNode;

            Debug.Log(RailGraphDatastore.GetDistanceBetweenNodes(railNodeA, railNodeB));
            var length0 = RailGraphDatastore.GetDistanceBetweenNodes(railNodeB, railNodeC);
            Debug.Log(length0);
            Assert.AreEqual(0, length0, "駅間のFrontNode距離が0になっていません。");
            Debug.Log(RailGraphDatastore.GetDistanceBetweenNodes(railNodeC, railNodeD));
        }
    }
}
