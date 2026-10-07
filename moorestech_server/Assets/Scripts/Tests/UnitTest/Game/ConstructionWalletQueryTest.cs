using System;
using System.Collections.Generic;
using Core.Item.Interface;
using Core.Master;
using Game.Construction;
using Game.Context;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UniRx;

namespace Tests.UnitTest.Game
{
    /// <summary>
    /// 財布の問い合わせ窓口の契約試験。サーバー・クライアント双方がこのクラスを実行する
    /// Contract tests for the wallet's query window, the very class both the server and the client run
    /// </summary>
    public class ConstructionWalletQueryTest
    {
        private const int PlayerId = 1;
        private static readonly Guid Material1Guid = Guid.Parse("00000000-0000-0000-1234-000000000003"); // Test3(コスト×2)
        private static readonly Guid Material2Guid = Guid.Parse("00000000-0000-0000-1234-000000000004"); // Test4(コスト×1)

        [Test]
        public void 財布が賄うセルは消費素材が空になる()
        {
            var query = CreateQuery(out var mutation);
            mutation.Refill(PlayerId, ForUnitTestModBlockId.GearBeltConveyor, 3);

            Assert.IsTrue(query.IsCoveredByWallet(ForUnitTestModBlockId.GearBeltConveyor));
            Assert.AreEqual(0, query.GetItemsToConsume(ForUnitTestModBlockId.GearBeltConveyor).Count);
        }

        [Test]
        public void 財布が空なら建設コスト全額を消費素材として返す()
        {
            var query = CreateQuery(out _);

            Assert.IsFalse(query.IsCoveredByWallet(ForUnitTestModBlockId.GearBeltConveyor));
            Assert.AreEqual(2, query.GetItemsToConsume(ForUnitTestModBlockId.GearBeltConveyor).Count);
        }

        [Test]
        public void 財布を使わないブロックの状態はnullになる()
        {
            var query = CreateQuery(out _);

            Assert.IsNull(query.GetWalletStatus(ForUnitTestModBlockId.BlockId));
        }

        [Test]
        public void 財布を使うブロックの状態は設置数と残数を運ぶ()
        {
            var query = CreateQuery(out var mutation);
            mutation.Refill(PlayerId, ForUnitTestModBlockId.GearBeltConveyor, 3);
            mutation.ConsumeOne(PlayerId, ForUnitTestModBlockId.GearBeltConveyor);

            // 坂ベルトIDで問い合わせても直線代表の財布を引く
            // Querying with the slope belt id still reads the straight block's wallet
            var status = query.GetWalletStatus(ForUnitTestModBlockId.TestGearBeltConveyorUp);

            Assert.IsNotNull(status);
            Assert.AreEqual(3, status.Value.PlacementsPerCost);
            Assert.AreEqual(2, status.Value.RemainingCount);
        }

        [Test]
        public void 残り設置数と買えるセット数から置ける数を算出する()
        {
            var query = CreateQuery(out var mutation);
            mutation.Refill(PlayerId, ForUnitTestModBlockId.GearBeltConveyor, 3);
            mutation.ConsumeOne(PlayerId, ForUnitTestModBlockId.GearBeltConveyor);
            mutation.ConsumeOne(PlayerId, ForUnitTestModBlockId.GearBeltConveyor);

            // 残1 + 素材2セット×3 = 7
            // One left in the wallet plus two affordable sets of three = 7
            Assert.AreEqual(7, query.GetAffordablePlacementCount(ForUnitTestModBlockId.GearBeltConveyor, CreateInventory(2, 2)));
        }

        [Test]
        public void 設置数1なら素材セル数がそのまま置ける数になる()
        {
            var query = CreateQuery(out _);

            Assert.AreEqual(2, query.GetAffordablePlacementCount(ForUnitTestModBlockId.BlockId, CreateInventory(5, 2)));
        }

        [Test]
        public void コスト未定義なら残り設置数に関わらずMaxValue()
        {
            var query = CreateQuery(out _);

            Assert.AreEqual(int.MaxValue, query.GetAffordablePlacementCount(ForUnitTestModBlockId.BeltConveyorId, new List<IItemStack>()));
        }

        [Test]
        public void 財布が動くと通知が飛ぶ()
        {
            var query = CreateQuery(out var mutation);

            var changedCount = 0;
            using (query.OnWalletChanged.Subscribe(_ => changedCount++))
            {
                mutation.Refill(PlayerId, ForUnitTestModBlockId.GearBeltConveyor, 3);
                mutation.FlushChanges();
            }

            Assert.AreEqual(1, changedCount);
        }

        [Test]
        public void 設置計画は坂ベルトの財布キーと補充または残数消費を決める()
        {
            var query = CreateQuery(out var mutation);
            var slope = ForUnitTestModBlockId.TestGearBeltConveyorUp;
            var wallet = ForUnitTestModBlockId.GearBeltConveyor;

            // 空の財布は素材を払い代表キーへ補充
            // An empty wallet pays materials and refills the representative key
            Assert.IsTrue(query.TryPlanCell(slope, out var paid));
            Assert.AreEqual(ConstructionWalletUsage.PaidAndRefilled, paid.Usage);
            Assert.AreEqual(wallet, paid.WalletBlockId);
            Assert.AreEqual(3, paid.PlacementsPerCost);
            Assert.AreEqual(2, paid.ItemsToConsume.Count);

            mutation.Refill(PlayerId, wallet, paid.PlacementsPerCost);
            mutation.ConsumeOne(PlayerId, wallet);
            Assert.IsTrue(query.TryPlanCell(slope, out var covered));
            Assert.AreEqual(ConstructionWalletUsage.CoveredByWallet, covered.Usage);
            Assert.IsEmpty(covered.ItemsToConsume);
        }

        [Test]
        public void 撤去の凝縮境界と返却素材を窓口が決める()
        {
            var query = CreateQuery(out var mutation);
            var slope = ForUnitTestModBlockId.TestGearBeltConveyorUp;
            var wallet = ForUnitTestModBlockId.GearBeltConveyor;

            // セット未満は財布、到達時だけ素材へ戻す
            // Return to the wallet below one set and refund materials only at the set boundary
            Assert.IsTrue(query.TryPlanRemovalCell(slope, out var accumulating));
            Assert.AreEqual(wallet, accumulating.WalletBlockId);
            Assert.IsFalse(accumulating.WouldCondense);
            Assert.IsEmpty(accumulating.ItemsToRefund);
            mutation.Refill(PlayerId, wallet, 3);
            mutation.ConsumeOne(PlayerId, wallet);
            Assert.IsTrue(query.TryPlanRemovalCell(slope, out var condensing));
            Assert.IsTrue(condensing.WouldCondense);
            Assert.AreEqual(2, condensing.ItemsToRefund.Count);

            // 財布対象外は設置も撤去も直接素材を使う
            // Non-wallet blocks use materials directly for both placement and removal
            Assert.IsFalse(query.UsesWallet(ForUnitTestModBlockId.BlockId));
            Assert.IsFalse(query.TryPlanCell(ForUnitTestModBlockId.BlockId, out _));
            Assert.IsFalse(query.TryPlanRemovalCell(ForUnitTestModBlockId.BlockId, out _));
            Assert.AreEqual(query.GetItemsToConsume(ForUnitTestModBlockId.BlockId), query.GetItemsToRefund(ForUnitTestModBlockId.BlockId));
        }

        private static ConstructionWalletQuery CreateQuery(out IRemainingPlacementCountMutation mutation)
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            mutation = serviceProvider.GetService<IRemainingPlacementCountMutation>();
            var lookup = serviceProvider.GetService<IRemainingPlacementCountLookup>();
            return new ConstructionWalletQuery(lookup.GetReader(PlayerId));
        }

        private static List<IItemStack> CreateInventory(int material1Count, int material2Count)
        {
            var factory = ServerContext.ItemStackFactory;
            var inventory = new List<IItemStack>();
            if (0 < material1Count) inventory.Add(factory.Create(MasterHolder.ItemMaster.GetItemId(Material1Guid), material1Count));
            if (0 < material2Count) inventory.Add(factory.Create(MasterHolder.ItemMaster.GetItemId(Material2Guid), material2Count));
            return inventory;
        }
    }
}
