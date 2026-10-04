using System;
using System.Linq;
using Core.Inventory;
using Core.Item;
using Core.Master;
using Game.Block.Interface;
using Game.Context;
using Game.PlayerInventory.Interface;
using Game.Train.RailGraph;
using Game.UnlockState;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using Game.World.Interface.DataStore;
using NUnit.Framework;
using Server.Protocol.PacketResponse;
using Server.Protocol.PacketResponse.Util.RailEdit;
using Tests.Module.TestMod;
using Tests.Util;
using UnityEngine;
using static Server.Protocol.PacketResponse.RemoveBlockProtocol;

namespace Tests.CombinedTest.Server.PacketTest
{
    public class RemoveRailBlockRefundTest
    {
        private const int PlayerId = 13;
        private static readonly Guid RailConnectToolGuid = Guid.Parse("c0000000-0000-0000-0000-000000000002");
        private static readonly Guid ReinforcingMaterialGuid = Guid.Parse("00000000-0000-0000-1234-000000000002");
        private static readonly Guid IronPlateGuid = Guid.Parse("00000000-0000-0000-1234-000000000003");
        // 返却素材と別種の詰め物（電線アイテム）
        // Filler item distinct from the refund materials (the wire item)
        private static readonly Guid FillerItemGuid = Guid.Parse("00000000-0000-0000-1234-000000000001");

        private TrainTestEnvironment _environment;
        private IOpenableInventory _inventory;
        private ItemId _reinforcingMaterialId;
        private ItemId _ironPlateId;

        [SetUp]
        public void SetUp()
        {
            // レール環境と解放済みレールconnectToolを準備する
            // Prepare the rail environment and the unlocked rail connectTool
            _environment = TrainTestHelper.CreateEnvironment();
            _inventory = _environment.ServiceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId).MainOpenableInventory;
            _environment.ServiceProvider.GetService<IGameUnlockStateDataController>().UnlockConnectTool(RailConnectToolGuid);
            _reinforcingMaterialId = MasterHolder.ItemMaster.GetItemId(ReinforcingMaterialGuid);
            _ironPlateId = MasterHolder.ItemMaster.GetItemId(IronPlateGuid);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void 橋脚または駅の撤去で付いていたレールの素材が返る(bool station)
        {
            // 同じ種類の未接続ブロックと比較してレール分だけを測る
            // Compare against an unconnected block of the same type to isolate the rail refund
            var blockId = station ? ForUnitTestModBlockId.TestTrainStation : ForUnitTestModBlockId.TestTrainRail;
            var (_, components) = TrainTestHelper.PlaceBlockWithRailComponents(_environment, blockId, Vector3Int.zero, BlockDirection.North);
            var railA = components[0];
            var railB = TrainTestHelper.PlaceRail(_environment, new Vector3Int(10, 0, 0), BlockDirection.North);
            TrainTestHelper.PlaceBlock(_environment, blockId, new Vector3Int(30, 0, 0), BlockDirection.North);
            var units = CalculateUnits(railA.FrontNode, railB.BackNode);
            ConnectWithTool(railA.FrontNode, railB.BackNode, units);

            // 接続の無い橋脚の撤去で、撤去そのものの返却分（建設コスト）を基準として測る
            // Measure the removal's own refund (construction cost) on an unconnected pier as the baseline
            var (baseReinforcing, baseIron) = RemoveAndMeasureGain(new Vector3Int(30, 0, 0));
            var (reinforcing, iron) = RemoveAndMeasureGain(Vector3Int.zero);

            Assert.AreEqual(baseReinforcing + units * 12, reinforcing);
            Assert.AreEqual(baseIron + units * 5, iron);
        }

        [Test]
        public void 両端の橋脚を撤去してもレール1本分しか返らない()
        {
            var railA = TrainTestHelper.PlaceRail(_environment, Vector3Int.zero, BlockDirection.North);
            var railB = TrainTestHelper.PlaceRail(_environment, new Vector3Int(10, 0, 0), BlockDirection.North);
            TrainTestHelper.PlaceRail(_environment, new Vector3Int(30, 0, 0), BlockDirection.North);
            var units = CalculateUnits(railA.FrontNode, railB.BackNode);
            ConnectWithTool(railA.FrontNode, railB.BackNode, units);

            // 先にA側を撤去してレールを返却させ、B側の撤去では基準分しか返らないことを確かめる
            // Remove A first so the rail is refunded there; removing B must then return only the baseline
            var (baseReinforcing, baseIron) = RemoveAndMeasureGain(new Vector3Int(30, 0, 0));
            RemoveAndMeasureGain(Vector3Int.zero);
            var (reinforcing, iron) = RemoveAndMeasureGain(new Vector3Int(10, 0, 0));

            Assert.AreEqual(baseReinforcing, reinforcing);
            Assert.AreEqual(baseIron, iron);
        }

        [Test]
        public void 駅内部の区間は無償なので返却しない()
        {
            // 駅内部の区間はGuid.Emptyで張られるため返却対象にならない
            // Station-internal segments carry Guid.Empty, so they are never refunded
            var station = TrainTestHelper.PlaceBlock(_environment, ForUnitTestModBlockId.TestTrainStation, Vector3Int.zero, BlockDirection.North);
            Assert.IsTrue(RailRemovalRefundCalculator.TryCreateRefundItems(station, _environment.GetRailGraphDatastore(), out var refundItems));

            Assert.AreEqual(0, refundItems.Count);
        }

        [Test]
        public void レール返却がインベントリに入らなければInventoryFullで撤去を拒否する()
        {
            var railA = TrainTestHelper.PlaceRail(_environment, Vector3Int.zero, BlockDirection.North);
            var railB = TrainTestHelper.PlaceRail(_environment, new Vector3Int(10, 0, 0), BlockDirection.North);
            var units = CalculateUnits(railA.FrontNode, railB.BackNode);
            ConnectWithTool(railA.FrontNode, railB.BackNode, units);

            // 返却素材と重ならない別アイテムの満杯スタックで全スロットを埋める
            // Fill every slot with full stacks of an unrelated item so the refund cannot merge anywhere
            var fillerItemId = MasterHolder.ItemMaster.GetItemId(FillerItemGuid);
            var fillerMaxStack = ItemStackLevelDataStore.Instance.GetMaxStack(fillerItemId);
            for (var i = 0; i < _inventory.GetSlotSize(); i++) _inventory.SetItem(i, ServerContext.ItemStackFactory.Create(fillerItemId, fillerMaxStack));

            var payload = MessagePackSerializer.Serialize(new RemoveBlockProtocolMessagePack(Vector3Int.zero));
            var responseBytes = _environment.PacketResponseCreator.GetPacketResponse(payload, Tests.Util.PlayerIdentity.BoundPacketContext.Bind(PlayerId)).First();
            var response = MessagePackSerializer.Deserialize<RemoveBlockResponseMessagePack>(responseBytes.ToArray());

            // 撤去されず、レールも残ることを確かめる
            // Verify nothing was removed and the rail is still there
            Assert.IsFalse(response.Success);
            Assert.AreEqual(RemoveBlockFailureReason.InventoryFull, response.FailureReason);
            Assert.IsTrue(_environment.WorldBlockDatastore.Exists(Vector3Int.zero));
            TrainTestHelper.Node2NodeCheckAndAssert(railA.FrontNode, railB.BackNode, "railA", "railB");
        }

        private int CalculateUnits(RailNode from, RailNode to)
        {
            var length = RailConnectionEditProtocol.GetRailLength(from, to);
            return Mathf.CeilToInt(length / 5f);
        }

        private void ConnectWithTool(RailNode from, RailNode to, int units)
        {
            // 接続に必要な素材をちょうど持たせ、レールconnectToolで接続する
            // Give exactly the required materials and connect with the rail connectTool
            _inventory.SetItem(0, ServerContext.ItemStackFactory.Create(_reinforcingMaterialId, units * 12));
            _inventory.SetItem(1, ServerContext.ItemStackFactory.Create(_ironPlateId, units * 5));
            var request = RailConnectionEditProtocol.RailConnectionEditRequest.CreateConnectRequest(from.NodeId, from.Guid, to.NodeId, to.Guid, RailConnectToolGuid);
            var responseBytes = _environment.PacketResponseCreator.GetPacketResponse(MessagePackSerializer.Serialize(request), Tests.Util.PlayerIdentity.BoundPacketContext.Bind(PlayerId)).First();
            var response = MessagePackSerializer.Deserialize<RailConnectionEditProtocol.ResponseRailConnectionEditMessagePack>(responseBytes.ToArray());
            Assert.IsTrue(response.Success, response.FailureReason.ToString());
        }

        private (int reinforcing, int iron) RemoveAndMeasureGain(Vector3Int position)
        {
            // インベントリを空にしてから撤去し、増えた分だけを返す
            // Empty the inventory, remove, and return only what was gained
            for (var i = 0; i < _inventory.GetSlotSize(); i++) _inventory.SetItem(i, ServerContext.ItemStackFactory.CreatEmpty());
            var payload = MessagePackSerializer.Serialize(new RemoveBlockProtocolMessagePack(position));
            var responseBytes = _environment.PacketResponseCreator.GetPacketResponse(payload, Tests.Util.PlayerIdentity.BoundPacketContext.Bind(PlayerId)).First();
            Assert.IsTrue(MessagePackSerializer.Deserialize<RemoveBlockResponseMessagePack>(responseBytes.ToArray()).Success);
            return (CountItem(_reinforcingMaterialId), CountItem(_ironPlateId));
        }

        private int CountItem(ItemId itemId)
        {
            return _inventory.InventoryItems.Where(stack => stack.Id == itemId).Sum(stack => stack.Count);
        }
    }
}
