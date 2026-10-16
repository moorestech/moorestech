using System;
using System.Collections.Generic;
using System.Linq;
using Core.Master;
using Game.Block.Blocks.TrainRail;
using Game.Block.Interface;
using Game.Context;
using Game.PlayerInventory.Interface;
using Game.Train.RailGraph;
using Game.Train.RailPositions;
using Game.Train.SaveLoad;
using Game.Train.Unit;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Util.MessagePack;
using Tests.Module.TestMod;
using Tests.Util;
using UnityEngine;
using static Server.Protocol.PacketResponse.PlaceTrainCarOnRailProtocol;

namespace Tests.CombinedTest.Server.PacketTest.TrainPlacement
{
    // 隣接する駅ブロックは距離0の辺で自動接続される。その辺を含む経路の設置は通り、未接続の経路だけが拒否されることを固定する
    // Adjacent station blocks are auto-linked by a zero-length edge; placement through that edge passes while disconnected routes stay rejected
    public class TrainPlacementAcrossStationBoundaryTest
    {
        private const int PlayerId = 1;

        [Test]
        public void 隣接駅の境界をまたぐ設置は距離0の辺を含んでも成功する()
        {
            var layout = SetupAdjacentStations();
            SupplyMaterials(layout.Environment);

            // 先頭は2駅目の内部、最後尾は1駅目の内部。中間に距離0の辺を含む
            // Head inside the 2nd station, rear inside the 1st, with the zero-length edge in between
            var half = layout.TrainLength / 2;
            var route = new RailPosition(new List<IRailNode> { layout.SecondExit, layout.SecondEntry, layout.FirstExit, layout.FirstEntry }, layout.TrainLength, layout.SecondInternalLength - half);
            var response = ExecutePlace(layout.Environment, route.CreateSaveSnapshot(), layout.TrainCarGuid);

            Assert.IsTrue(response.Success, "境界をまたぐ設置は成功するべき / Placement across the boundary should succeed");
            Assert.AreEqual(1, layout.Environment.GetITrainLookupDatastore().GetRegisteredTrains().Count);
        }

        [Test]
        public void 隣の駅のノードを起点にした経路でもぴったり収まる設置は成功する()
        {
            var layout = SetupAdjacentStations();
            SupplyMaterials(layout.Environment);

            // 駅スナップが隣の駅の出口を起点に選ぶと、末尾に距離0の辺が付いた経路になる
            // When the station snap starts from the neighbour's exit, the route carries a trailing zero-length edge
            var route = new RailPosition(new List<IRailNode> { layout.SecondExit, layout.SecondEntry, layout.FirstExit }, layout.TrainLength, 0);
            var response = ExecutePlace(layout.Environment, route.CreateSaveSnapshot(), layout.TrainCarGuid);

            Assert.IsTrue(response.Success, "末尾の距離0の辺は設置を妨げないべき / A trailing zero-length edge must not block placement");
            Assert.AreEqual(1, layout.Environment.GetITrainLookupDatastore().GetRegisteredTrains().Count);
        }

        [Test]
        public void 未接続のノード対を含む経路はInvalidRailPositionで拒否される()
        {
            var layout = SetupAdjacentStations();
            SupplyMaterials(layout.Environment);

            // 2駅目の出口と1駅目の入口は直接つながっていない
            // The 2nd exit and the 1st entry are not directly connected
            // RailPosition は組み立て時に長さ不足を弾くので、保存形式を直接作って送る
            // RailPosition rejects a short route on construction, so build the save data directly
            var saveData = new RailPositionSaveData
            {
                TrainLength = layout.TrainLength,
                DistanceToNextNode = 0,
                RailSnapshot = new List<ConnectionDestination> { layout.SecondExit.ConnectionDestination, layout.FirstEntry.ConnectionDestination },
            };
            var response = ExecutePlace(layout.Environment, saveData, layout.TrainCarGuid);

            Assert.IsFalse(response.Success, "未接続の経路は拒否されるべき / A disconnected route must be rejected");
            Assert.AreEqual(PlaceTrainCarFailureType.InvalidRailPosition, response.FailureType);
            Assert.AreEqual(0, layout.Environment.GetITrainLookupDatastore().GetRegisteredTrains().Count);
        }

        #region TestUtil

        private readonly struct AdjacentStationLayout
        {
            public AdjacentStationLayout(TrainTestEnvironment environment, RailNode firstEntry, RailNode firstExit, RailNode secondEntry, RailNode secondExit, int secondInternalLength, int trainLength, Guid trainCarGuid)
            {
                Environment = environment;
                FirstEntry = firstEntry;
                FirstExit = firstExit;
                SecondEntry = secondEntry;
                SecondExit = secondExit;
                SecondInternalLength = secondInternalLength;
                TrainLength = trainLength;
                TrainCarGuid = trainCarGuid;
            }

            public TrainTestEnvironment Environment { get; }
            public RailNode FirstEntry { get; }
            public RailNode FirstExit { get; }
            public RailNode SecondEntry { get; }
            public RailNode SecondExit { get; }
            public int SecondInternalLength { get; }
            public int TrainLength { get; }
            public Guid TrainCarGuid { get; }
        }

        private static AdjacentStationLayout SetupAdjacentStations()
        {
            // 駅を2つ進行方向に隣接させ、自動接続された距離0の辺の両端を探す
            // Place two stations end to end and locate both ends of the auto-linked zero-length edge
            var environment = TrainTestHelper.CreateEnvironment();
            var stationLength = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.TestTrainStation).BlockSize.z;
            var (_, first) = TrainTestHelper.PlaceBlockWithRailComponents(environment, ForUnitTestModBlockId.TestTrainStation, Vector3Int.zero, BlockDirection.East);
            var (_, second) = TrainTestHelper.PlaceBlockWithRailComponents(environment, ForUnitTestModBlockId.TestTrainStation, new Vector3Int(stationLength, 0, 0), BlockDirection.East);
            var graph = environment.GetRailGraphDatastore();
            var firstNodes = new[] { first[0].FrontNode, first[0].BackNode, first[1].FrontNode, first[1].BackNode };
            var secondNodes = new[] { second[0].FrontNode, second[0].BackNode, second[1].FrontNode, second[1].BackNode };

            var (firstExit, secondEntry) = FindZeroLengthLink();
            var (firstEntry, _) = FindInternalNeighbour(firstNodes, firstExit, true);
            var (secondExit, secondInternalLength) = FindInternalNeighbour(secondNodes, secondEntry, false);

            var trainCarMaster = MasterHolder.TrainUnitMaster.Train.TrainCars[0];
            var trainLength = TrainLengthConverter.ToRailUnits(trainCarMaster.Length);
            Assert.AreEqual(secondInternalLength, trainLength, "テスト用マスタでは駅内部長と車両長が一致する前提 / The test master pairs the station length with the car length");
            return new AdjacentStationLayout(environment, firstEntry, firstExit, secondEntry, secondExit, secondInternalLength, trainLength, trainCarMaster.TrainCarGuid);

            #region Internal

            (RailNode from, RailNode to) FindZeroLengthLink()
            {
                foreach (var a in firstNodes)
                foreach (var b in secondNodes)
                    if (graph.GetDistanceBetweenNodes(a, b) == 0) return (a, b);
                Assert.Fail("隣接駅の距離0接続が見つからない / Zero-length link between adjacent stations not found");
                return (null, null);
            }

            // anchorへ向かう辺(towardAnchor)か、anchorから出る辺かで駅内部の相手ノードを探す
            // Find the internal partner node by the edge into the anchor (towardAnchor) or out of it
            (RailNode node, int distance) FindInternalNeighbour(RailNode[] candidates, RailNode anchor, bool towardAnchor)
            {
                foreach (var node in candidates)
                {
                    var distance = towardAnchor ? graph.GetDistanceBetweenNodes(node, anchor) : graph.GetDistanceBetweenNodes(anchor, node);
                    if (0 < distance) return (node, distance);
                }
                Assert.Fail("駅内部の辺が見つからない / Station internal edge not found");
                return (null, -1);
            }

            #endregion
        }

        private static void SupplyMaterials(TrainTestEnvironment environment)
        {
            var inventory = environment.ServiceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId).MainOpenableInventory;
            inventory.SetItem(0, ServerContext.ItemStackFactory.Create(ForUnitTestItemId.ItemId3, 3));
            inventory.SetItem(1, ServerContext.ItemStackFactory.Create(ForUnitTestItemId.ItemId4, 2));
        }

        private static PlaceTrainOnRailResponseMessagePack ExecutePlace(TrainTestEnvironment environment, RailPositionSaveData saveData, Guid trainCarGuid)
        {
            var packet = MessagePackSerializer.Serialize(new PlaceTrainOnRailRequestMessagePack(new RailPositionSnapshotMessagePack(saveData), trainCarGuid));
            var responses = environment.PacketResponseCreator.GetPacketResponse(packet, Tests.Util.PlayerIdentity.BoundPacketContext.Bind(PlayerId));
            return MessagePackSerializer.Deserialize<PlaceTrainOnRailResponseMessagePack>(responses[0]);
        }

        #endregion
    }
}
