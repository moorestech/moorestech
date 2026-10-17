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
using static Server.Protocol.PacketResponse.AttachTrainCarToUnitProtocol;
using static Server.Protocol.PacketResponse.PlaceTrainCarOnRailProtocol;

namespace Tests.CombinedTest.Server.PacketTest.TrainPlacement
{
    // 隣接する駅ブロックは距離0の辺で自動接続される。その辺を含む経路の設置・連結は通り、未接続の経路だけが拒否されることを固定する
    // Adjacent station blocks are auto-linked by a zero-length edge; placement and attachment through that edge pass while disconnected routes stay rejected
    public class TrainPlacementAcrossStationBoundaryTest
    {
        private const int PlayerId = 1;

        [Test]
        public void 隣接駅の境界をまたぐ設置は距離0の辺を含んでも成功する()
        {
            var layout = AdjacentStationLayout.Setup();
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
            var layout = AdjacentStationLayout.Setup();
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
            var layout = AdjacentStationLayout.Setup();
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

        [Test]
        public void 駅境界に止まる編成の先頭へ隣駅ノードまで含む経路の車両を連結できる()
        {
            var layout = AdjacentStationLayout.Setup();
            SupplyMaterials(layout.Environment);

            // 1駅目にぴったり収まる編成を置く。先頭は駅境界のノード上
            // Place a train that exactly fills the 1st station; its head sits on the boundary node
            var target = new RailPosition(new List<IRailNode> { layout.FirstExit, layout.FirstEntry }, layout.TrainLength, 0);
            Assert.IsTrue(ExecutePlace(layout.Environment, target.CreateSaveSnapshot(), layout.TrainCarGuid).Success);
            var targetTrain = layout.Environment.GetITrainLookupDatastore().GetRegisteredTrains().Last();

            // 2駅目に収まる車両を、隣の駅の出口まで含んだ経路（連結スナップが作る形）で先頭へ連結する
            // Attach a car filling the 2nd station with the route the attach snap produces, reaching back to the neighbour's exit
            SupplyMaterials(layout.Environment);
            var attaching = new RailPositionSaveData
            {
                TrainLength = layout.TrainLength,
                DistanceToNextNode = 0,
                RailSnapshot = new List<ConnectionDestination> { layout.SecondExit.ConnectionDestination, layout.SecondEntry.ConnectionDestination, layout.FirstExit.ConnectionDestination },
            };
            var packet = MessagePackSerializer.Serialize(new AttachTrainCarToUnitRequestMessagePack(targetTrain.TrainUnitInstanceId, new RailPositionSnapshotMessagePack(attaching), layout.TrainCarGuid, false, true));
            var responses = layout.Environment.PacketResponseCreator.GetPacketResponse(packet, Tests.Util.PlayerIdentity.BoundPacketContext.Bind(PlayerId));
            Assert.AreEqual(1, responses.Count, "連結は応答を返すべき（例外で握られていない） / Attachment must answer instead of throwing");
            var response = MessagePackSerializer.Deserialize<AttachTrainCarToUnitResponseMessagePack>(responses[0]);

            Assert.IsTrue(response.Success, $"境界をまたぐ連結は成功するべき / Attachment across the boundary should succeed: {response.FailureType}");
            Assert.AreEqual(2, targetTrain.Cars.Count);
            // 連結後の編成の位置は、本体の外に出る距離0ノードを持たない
            // After attachment the train position carries no zero-length node outside its body
            var nodes = targetTrain.RailPosition.GetRailNodes();
            Assert.IsTrue(nodes[nodes.Count - 1].GetDistanceToNode(nodes[nodes.Count - 2]) > 0, "最後尾の外に距離0ノードが残らないべき / No zero-length node should trail the rear");
        }

        #region TestUtil

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
