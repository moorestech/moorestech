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

namespace Tests.UnitTest.Server
{
    public class ElectricWirePlacementEvaluatorTest
    {
        // TestModのelectricWireItemsに登録済みのアイテムGuid（consumptionPerLength: 1）
        // Item guid registered in TestMod's electricWireItems (consumptionPerLength: 1)
        private static readonly Guid WireItemGuid = new("00000000-0000-0000-4649-000000000001");

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
        public void 距離が上限を超えるとTooFarになる()
        {
            var inventory = CreateInventory(_wireItemId, 100);

            var judgement = ElectricWirePlacementEvaluator.EvaluateWireConnection(
                15f, 10f, 12f, false, false, _wireItemId, inventory, ItemMaster.EmptyItemId);

            Assert.False(judgement.IsPlaceable);
            Assert.AreEqual(ElectricWirePlacementEvaluator.TooFarError, judgement.FailureReason);
        }

        [Test]
        public void 既に接続済みならAlreadyConnectedになる()
        {
            var inventory = CreateInventory(_wireItemId, 100);

            var judgement = ElectricWirePlacementEvaluator.EvaluateWireConnection(
                5f, 10f, 12f, true, false, _wireItemId, inventory, ItemMaster.EmptyItemId);

            Assert.False(judgement.IsPlaceable);
            Assert.AreEqual(ElectricWirePlacementEvaluator.AlreadyConnectedError, judgement.FailureReason);
        }

        [Test]
        public void 接続数が上限に達しているとConnectionLimitになる()
        {
            var inventory = CreateInventory(_wireItemId, 100);

            var judgement = ElectricWirePlacementEvaluator.EvaluateWireConnection(
                5f, 10f, 12f, false, true, _wireItemId, inventory, ItemMaster.EmptyItemId);

            Assert.False(judgement.IsPlaceable);
            Assert.AreEqual(ElectricWirePlacementEvaluator.ConnectionLimitError, judgement.FailureReason);
        }

        [Test]
        public void 電線アイテムが不足しているとNoWireItemになる()
        {
            // 距離5に対して必要数は5だが、所持数は2しかない
            // Distance 5 requires 5 items, but inventory only has 2
            var inventory = CreateInventory(_wireItemId, 2);

            var judgement = ElectricWirePlacementEvaluator.EvaluateWireConnection(
                5f, 10f, 12f, false, false, _wireItemId, inventory, ItemMaster.EmptyItemId);

            Assert.False(judgement.IsPlaceable);
            Assert.AreEqual(ElectricWirePlacementEvaluator.NoWireItemError, judgement.FailureReason);
        }

        [Test]
        public void ポールアイテムを所持していないとNoPoleItemになる()
        {
            var inventory = CreateInventory(_wireItemId, 100);

            var judgement = ElectricWirePlacementEvaluator.EvaluateWireConnection(
                5f, 10f, 12f, false, false, _wireItemId, inventory, ForUnitTestItemId.ItemId1);

            Assert.False(judgement.IsPlaceable);
            Assert.AreEqual(ElectricWirePlacementEvaluator.NoPoleItemError, judgement.FailureReason);
        }

        [Test]
        public void 条件を満たすと接続可能でコストは距離を切り上げた数になる()
        {
            // consumptionPerLengthは1のため、距離5.5は切り上げで6必要
            // consumptionPerLength is 1, so distance 5.5 rounds up to a cost of 6
            var inventory = CreateInventory(_wireItemId, 6);

            var judgement = ElectricWirePlacementEvaluator.EvaluateWireConnection(
                5.5f, 10f, 12f, false, false, _wireItemId, inventory, ItemMaster.EmptyItemId);

            Assert.True(judgement.IsPlaceable);
            Assert.AreEqual(string.Empty, judgement.FailureReason);
            Assert.AreEqual(_wireItemId, judgement.WireCost.ItemId);
            Assert.AreEqual(6, judgement.WireCost.Count);
        }

        [Test]
        public void TryCalculateWireCostは距離を切り上げてコストを算出する()
        {
            var succeeded = ElectricWirePlacementEvaluator.TryCalculateWireCost(_wireItemId, 3.2f, out var cost);

            Assert.True(succeeded);
            Assert.AreEqual(_wireItemId, cost.ItemId);
            Assert.AreEqual(4, cost.Count);
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
