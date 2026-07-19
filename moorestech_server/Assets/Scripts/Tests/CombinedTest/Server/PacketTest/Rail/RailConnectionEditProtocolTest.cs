using System;
using System.Linq;
using Core.Inventory;
using Core.Master;
using Game.Block.Interface;
using Game.Context;
using Game.PlayerInventory.Interface;
using Game.Train.RailGraph;
using Game.UnlockState;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Protocol;
using Server.Protocol.PacketResponse;
using Tests.Util;
using UnityEngine;

namespace Tests.CombinedTest.Server.PacketTest.Rail
{
    public class RailConnectionEditProtocolTest
    {
        private const int PlayerId = 11;
        private static readonly Guid ConnectToolGuid = Guid.Parse("c0000000-0000-0000-0000-000000000002");
        private static readonly Guid ReinforcingMaterialGuid = Guid.Parse("00000000-0000-0000-1234-000000000002");
        private static readonly Guid IronPlateGuid = Guid.Parse("00000000-0000-0000-1234-000000000003");

        private TrainTestEnvironment _environment;
        private IOpenableInventory _inventory;
        private RailNode _fromNode;
        private RailNode _toNode;
        private ItemId _reinforcingMaterialId;
        private ItemId _ironPlateId;

        [SetUp]
        public void SetUp()
        {
            // 接続対象のレール端点と解放済みconnectToolを準備する
            // Prepare rail endpoints and the unlocked connectTool
            _environment = TrainTestHelper.CreateEnvironment();
            var fromRail = TrainTestHelper.PlaceRail(_environment, Vector3Int.zero, BlockDirection.North);
            var toRail = TrainTestHelper.PlaceRail(_environment, new Vector3Int(10, 0, 0), BlockDirection.North);
            _fromNode = fromRail.FrontNode;
            _toNode = toRail.BackNode;
            _inventory = _environment.ServiceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId).MainOpenableInventory;
            _environment.ServiceProvider.GetService<IGameUnlockStateDataController>().UnlockConnectTool(ConnectToolGuid);
            _reinforcingMaterialId = MasterHolder.ItemMaster.GetItemId(ReinforcingMaterialGuid);
            _ironPlateId = MasterHolder.ItemMaster.GetItemId(IronPlateGuid);
        }

        [Test]
        public void 複数素材のrailConnectToolで接続すると両素材を距離単位分消費する()
        {
            var units = CalculateUnits();
            var reinforcingCount = units * 12;
            var ironPlateCount = units * 5;
            SetInventory(reinforcingCount + 12, ironPlateCount + 5);

            var response = SendConnect();

            Assert.IsTrue(response.Success, response.FailureReason.ToString());
            Assert.AreEqual(12, CountItem(_reinforcingMaterialId));
            Assert.AreEqual(5, CountItem(_ironPlateId));
            TrainTestHelper.Node2NodeCheckAndAssert(_fromNode, _toNode, "fromNode", "toNode");
        }

        [Test]
        public void 複数素材の片方が不足すると接続に失敗し何も消費しない()
        {
            var units = CalculateUnits();
            var reinforcingCount = units * 12;
            var insufficientIronPlateCount = units * 5 - 1;
            SetInventory(reinforcingCount, insufficientIronPlateCount);

            var response = SendConnect();

            Assert.IsFalse(response.Success);
            Assert.AreEqual(RailConnectionEditProtocol.RailConnectionEditFailureReason.NotEnoughRailItem, response.FailureReason);
            Assert.AreEqual(reinforcingCount, CountItem(_reinforcingMaterialId));
            Assert.AreEqual(insufficientIronPlateCount, CountItem(_ironPlateId));
        }

        private int CalculateUnits()
        {
            var length = RailConnectionEditProtocol.GetRailLength(_fromNode, _toNode);
            return Mathf.CeilToInt(length / 5f);
        }

        private void SetInventory(int reinforcingCount, int ironPlateCount)
        {
            _inventory.SetItem(0, ServerContext.ItemStackFactory.Create(_reinforcingMaterialId, reinforcingCount));
            _inventory.SetItem(1, ServerContext.ItemStackFactory.Create(_ironPlateId, ironPlateCount));
        }

        private RailConnectionEditProtocol.ResponseRailConnectionEditMessagePack SendConnect()
        {
            var request = RailConnectionEditProtocol.RailConnectionEditRequest.CreateConnectRequest(
                PlayerId, _fromNode.NodeId, _fromNode.Guid, _toNode.NodeId, _toNode.Guid, ConnectToolGuid);
            var responseBytes = _environment.PacketResponseCreator.GetPacketResponse(
                MessagePackSerializer.Serialize(request), new PacketResponseContext(null)).First();
            return MessagePackSerializer.Deserialize<RailConnectionEditProtocol.ResponseRailConnectionEditMessagePack>(responseBytes.ToArray());
        }

        private int CountItem(ItemId itemId)
        {
            return _inventory.InventoryItems.Where(stack => stack.Id == itemId).Sum(stack => stack.Count);
        }
    }
}
