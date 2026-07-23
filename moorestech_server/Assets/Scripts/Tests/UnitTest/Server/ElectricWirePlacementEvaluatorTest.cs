using System;
using System.Collections.Generic;
using Core.Item.Interface;
using Core.Master;
using Game.Context;
using NUnit.Framework;
using Server.Boot;
using Server.Protocol.PacketResponse.Util.ElectricWire;
using Tests.Module;
using Tests.Module.TestMod;

using Server.Protocol.PacketResponse.Util.ElectricWire.Placement;

namespace Tests.UnitTest.Server
{
    public class ElectricWirePlacementEvaluatorTest
    {
        private static readonly Guid ConnectToolGuid = new("c0000000-0000-0000-0000-000000000001");
        private static readonly Guid WireItemGuid = new("00000000-0000-0000-1234-000000000001");

        private ItemId _wireItemId;

        [SetUp]
        public void SetUp()
        {
            // マスタデータを含むサーバーコンテキストを構築する
            // Build server context including master data
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            _wireItemId = MasterHolder.ItemMaster.GetItemId(WireItemGuid);
        }

        [Test]
        public void 既に接続済みならAlreadyConnectedになる()
        {
            var inventory = CreateInventory(_wireItemId, 100);

            var judgement = ElectricWirePlacementEvaluator.EvaluateWireConnection(
                5f, true, false, ConnectToolGuid, inventory, null);

            Assert.False(judgement.IsPlaceable);
            Assert.AreEqual(ElectricWirePlacementFailureReason.AlreadyConnected, judgement.FailureReason);
        }

        [Test]
        public void 接続数が上限に達しているとConnectionLimitになる()
        {
            var inventory = CreateInventory(_wireItemId, 100);

            var judgement = ElectricWirePlacementEvaluator.EvaluateWireConnection(
                5f, false, true, ConnectToolGuid, inventory, null);

            Assert.False(judgement.IsPlaceable);
            Assert.AreEqual(ElectricWirePlacementFailureReason.ConnectionLimit, judgement.FailureReason);
        }

        [Test]
        public void 電線アイテムが不足しているとNoWireItemになる()
        {
            // 距離5に対して必要数は5だが、所持数は2しかない
            // Distance 5 requires 5 items, but inventory only has 2
            var inventory = CreateInventory(_wireItemId, 2);

            var judgement = ElectricWirePlacementEvaluator.EvaluateWireConnection(
                5f, false, false, ConnectToolGuid, inventory, null);

            Assert.False(judgement.IsPlaceable);
            Assert.AreEqual(ElectricWirePlacementFailureReason.NoWireItem, judgement.FailureReason);
        }

        [Test]
        public void 別素材の予約は電線必要数に影響しない()
        {
            var inventory = CreateInventory(_wireItemId, 100);

            var judgement = ElectricWirePlacementEvaluator.EvaluateWireConnection(
                5f, false, false, ConnectToolGuid, inventory,
                new List<ConnectToolMaterialCost> { new(ForUnitTestItemId.ItemId1, 1) });

            Assert.True(judgement.IsPlaceable);
        }

        [Test]
        public void 条件を満たすと接続可能でコストは距離を切り上げた数になる()
        {
            // consumptionPerLengthは1のため、距離5.5は切り上げで6必要
            // consumptionPerLength is 1, so distance 5.5 rounds up to a cost of 6
            var inventory = CreateInventory(_wireItemId, 6);

            var judgement = ElectricWirePlacementEvaluator.EvaluateWireConnection(
                5.5f, false, false, ConnectToolGuid, inventory, null);

            Assert.True(judgement.IsPlaceable);
            Assert.AreEqual(ElectricWirePlacementFailureReason.None, judgement.FailureReason);
            Assert.AreEqual(_wireItemId, judgement.WireCost.Materials[0].ItemId);
            Assert.AreEqual(6, judgement.WireCost.Materials[0].Count);
        }

        [Test]
        public void ポールと電線が同一アイテムなら1個上乗せで不足判定される()
        {
            // 距離5でコスト5＋ポール分1＝6必要だが、所持数は5しかない
            // Distance 5 costs 5 wires plus 1 for the pole = 6, but only 5 are held
            var inventory = CreateInventory(_wireItemId, 5);

            var judgement = ElectricWirePlacementEvaluator.EvaluateWireConnection(
                5f, false, false, ConnectToolGuid, inventory,
                new List<ConnectToolMaterialCost> { new(_wireItemId, 1) });

            Assert.False(judgement.IsPlaceable);
            Assert.AreEqual(ElectricWirePlacementFailureReason.NoWireItem, judgement.FailureReason);
        }

        [Test]
        public void ポールと電線が同一アイテムで合算所持していれば接続可能になる()
        {
            // コスト5＋ポール分1＝6を所持していれば通過する
            // Passes when holding cost 5 plus 1 for the pole = 6
            var inventory = CreateInventory(_wireItemId, 6);

            var judgement = ElectricWirePlacementEvaluator.EvaluateWireConnection(
                5f, false, false, ConnectToolGuid, inventory,
                new List<ConnectToolMaterialCost> { new(_wireItemId, 1) });

            Assert.True(judgement.IsPlaceable);
            Assert.AreEqual(5, judgement.WireCost.TotalCount);
        }

        [Test]
        public void TryCalculateWireCostは距離を切り上げてコストを算出する()
        {
            var succeeded = ElectricWirePlacementEvaluator.TryCalculateWireCost(ConnectToolGuid, 3.2f, out var cost);

            Assert.True(succeeded);
            Assert.AreEqual(_wireItemId, cost.Materials[0].ItemId);
            Assert.AreEqual(4, cost.Materials[0].Count);
        }

        private static List<IItemStack> CreateInventory(ItemId itemId, int count)
        {
            // インベントリ内アイテムのスタブリストを生成する
            // Create a stub list representing inventory items
            var itemStack = ServerContext.ItemStackFactory.Create(itemId, count);
            return new List<IItemStack> { itemStack };
        }
    }
}
