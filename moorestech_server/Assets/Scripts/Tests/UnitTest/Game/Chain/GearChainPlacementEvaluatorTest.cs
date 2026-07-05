using System;
using Core.Item.Interface;
using Core.Master;
using Game.Context;
using NUnit.Framework;
using Server.Boot;
using Server.Protocol.PacketResponse.Util.GearChain;
using Tests.Module;
using Tests.Module.TestMod;

namespace Tests.UnitTest.Game.Chain
{
    public class GearChainPlacementEvaluatorTest
    {
        // 予約なし（既存ポール同士の接続用）を表す空リスト
        // Empty list representing no reservations (for pole-to-pole connection)
        private static readonly (ItemId itemId, int count)[] NoReserved = Array.Empty<(ItemId, int)>();

        private ItemId _chainItemId;
        private ItemId _poleItemId;

        [SetUp]
        public void SetUp()
        {
            // マスタロードのためDIコンテナを初期化
            // Initialize DI container to load master data
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            _chainItemId = MasterHolder.ItemMaster.GetItemId(ChainConstants.ChainItemGuid);
            _poleItemId = MasterHolder.BlockMaster.GetItemId(ForUnitTestModBlockId.GearChainPole);
        }

        [Test]
        public void EvaluateFailsWhenDistanceTooFar()
        {
            // 距離が両端上限のminを超えるケースを判定する
            // Judge a case where distance exceeds the min of both limits
            var judgement = GearChainPlacementEvaluator.EvaluatePlacement(11f, 10f, 20f, false, false, _chainItemId, Items((_chainItemId, 20)), NoReserved);
            Assert.False(judgement.IsPlaceable);
            Assert.AreEqual(GearChainPlacementEvaluator.TooFarError, judgement.FailureReason);
        }

        [Test]
        public void EvaluateFailsWhenAlreadyConnected()
        {
            // 既に接続済みのペアを判定する
            // Judge a pair that is already connected
            var judgement = GearChainPlacementEvaluator.EvaluatePlacement(3f, 10f, 10f, true, false, _chainItemId, Items((_chainItemId, 20)), NoReserved);
            Assert.False(judgement.IsPlaceable);
            Assert.AreEqual(GearChainPlacementEvaluator.AlreadyConnectedError, judgement.FailureReason);
        }

        [Test]
        public void EvaluateFailsWhenConnectionFull()
        {
            // 接続数上限に達しているケースを判定する
            // Judge a case where the connection count is full
            var judgement = GearChainPlacementEvaluator.EvaluatePlacement(3f, 10f, 10f, false, true, _chainItemId, Items((_chainItemId, 20)), NoReserved);
            Assert.False(judgement.IsPlaceable);
            Assert.AreEqual(GearChainPlacementEvaluator.ConnectionLimitError, judgement.FailureReason);
        }

        [Test]
        public void EvaluateFailsWhenChainItemIsNotEnough()
        {
            // 距離3にチェーン2個のみのケースを判定
            // Judge a case owning only 2 chains for distance 3
            var judgement = GearChainPlacementEvaluator.EvaluatePlacement(3f, 10f, 10f, false, false, _chainItemId, Items((_chainItemId, 2)), NoReserved);
            Assert.False(judgement.IsPlaceable);
            Assert.AreEqual(GearChainPlacementEvaluator.NoItemError, judgement.FailureReason);
        }

        [Test]
        public void EvaluateFailsWhenItemIsNotChainItem()
        {
            // チェーン設定外アイテム指定のケースを判定
            // Judge a case specifying an item not in the chain master
            var judgement = GearChainPlacementEvaluator.EvaluatePlacement(3f, 10f, 10f, false, false, _poleItemId, Items((_poleItemId, 20)), NoReserved);
            Assert.False(judgement.IsPlaceable);
            Assert.AreEqual(GearChainPlacementEvaluator.NoItemError, judgement.FailureReason);
        }

        [Test]
        public void EvaluateFailsWhenReservedChainItemLeavesTooFewChains()
        {
            // 予約リストにチェーンと同一アイテムがあると必要数に上乗せされる（3個では不足、4個で成功）
            // A reserved entry matching the chain item adds to the requirement (3 fails, 4 succeeds)
            var shortage = GearChainPlacementEvaluator.EvaluatePlacement(3f, 10f, 10f, false, false, _chainItemId, Items((_chainItemId, 3)), Reserved((_chainItemId, 1)));
            Assert.False(shortage.IsPlaceable);
            Assert.AreEqual(GearChainPlacementEvaluator.NoItemError, shortage.FailureReason);

            var enough = GearChainPlacementEvaluator.EvaluatePlacement(3f, 10f, 10f, false, false, _chainItemId, Items((_chainItemId, 4)), Reserved((_chainItemId, 1)));
            Assert.True(enough.IsPlaceable);
        }

        [Test]
        public void EvaluateIgnoresReservedItemDifferentFromChain()
        {
            // 予約がチェーンと別アイテムなら必要数へ影響しない
            // A reservation of a different item than the chain does not affect the requirement
            var judgement = GearChainPlacementEvaluator.EvaluatePlacement(3f, 10f, 10f, false, false, _chainItemId, Items((_chainItemId, 3)), Reserved((_poleItemId, 2)));
            Assert.True(judgement.IsPlaceable);
        }

        [Test]
        public void EvaluateSucceedsWithChainCost()
        {
            // すべての条件を満たすケースで消費コストを検証する
            // Verify chain cost in a fully valid case
            var judgement = GearChainPlacementEvaluator.EvaluatePlacement(3f, 10f, 10f, false, false, _chainItemId, Items((_chainItemId, 5)), NoReserved);
            Assert.True(judgement.IsPlaceable);
            Assert.AreEqual(_chainItemId, judgement.ChainCost.ItemId);
            Assert.AreEqual(3, judgement.ChainCost.Count);
        }

        private static (ItemId itemId, int count)[] Reserved(params (ItemId id, int count)[] items)
        {
            var result = new (ItemId itemId, int count)[items.Length];
            for (var i = 0; i < items.Length; i++) result[i] = (items[i].id, items[i].count);
            return result;
        }

        private static IItemStack[] Items(params (ItemId id, int count)[] items)
        {
            // 指定内容のインベントリ相当スタック配列を生成する
            // Build stack array equivalent to an inventory
            var stacks = new IItemStack[items.Length];
            for (var i = 0; i < items.Length; i++)
            {
                stacks[i] = ServerContext.ItemStackFactory.Create(items[i].id, items[i].count);
            }

            return stacks;
        }
    }
}
