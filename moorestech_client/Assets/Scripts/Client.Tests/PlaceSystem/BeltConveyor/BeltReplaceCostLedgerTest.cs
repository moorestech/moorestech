using System;
using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Replace.Cost;
using Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Replace.Cost.Plan;
using Client.Game.InGame.Construction;
using Core.Item.Interface;
using Core.Master;
using Game.Construction;
using Game.Context;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;

namespace Client.Tests.PlaceSystem.BeltConveyor
{
    /// <summary>
    /// 張替え台帳の残数遷移が、サーバーの財布と同じ関数を経由して同じ値を辿ることを検証する
    /// Verifies the replace ledger's remainder walks the very same values as the server's wallet, through the very same functions
    /// </summary>
    public class BeltReplaceCostLedgerTest
    {
        private const int PlayerId = 1;

        private static readonly Guid Material1Guid = Guid.Parse("00000000-0000-0000-1234-000000000003");
        private static readonly Guid Material2Guid = Guid.Parse("00000000-0000-0000-1234-000000000004");

        [Test]
        public void 台帳とサーバー財布は設置と撤去で同じ残数を辿る()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var store = serviceProvider.GetService<RemainingPlacementCountDataStore>();
            var wallet = ForUnitTestModBlockId.GearBeltConveyor;
            var placementsPerCost = MasterHolder.BlockMaster.GetBlockMaster(wallet).PlacementsPerCost;
            var ledger = new BeltReplaceCostLedger(BuildWalletQuery(), Array.Empty<IItemStack>());

            // 素材を払う設置 → 財布で賄う設置 → 未達の返却 → 凝縮する返却、の順で両者を同じだけ進める
            // Advance both sides through a paid placement, a wallet-covered one, a return below the threshold and a condensing one
            store.ApplyPlacement(PlayerId, wallet, placementsPerCost, ConstructionWalletUsage.PaidAndRefilled);
            new WalletBeltReplacePlacementPlan(CreateCostSets(1), wallet, placementsPerCost, false).Commit(ledger);
            AssertSameRemainder();

            store.ApplyPlacement(PlayerId, wallet, placementsPerCost, ConstructionWalletUsage.CoveredByWallet);
            new WalletBeltReplacePlacementPlan(Array.Empty<(ItemId, int)>(), wallet, placementsPerCost, true).Commit(ledger);
            AssertSameRemainder();

            store.ApplyReturn(PlayerId, wallet, false);
            new WalletBeltReplaceRemovalPlan(Array.Empty<IItemStack>(), wallet, false).Commit(ledger);
            AssertSameRemainder();

            store.ApplyReturn(PlayerId, wallet, true);
            new WalletBeltReplaceRemovalPlan(Array.Empty<IItemStack>(), wallet, true).Commit(ledger);
            AssertSameRemainder();

            void AssertSameRemainder()
            {
                Assert.AreEqual(store.GetRemainingCount(PlayerId, wallet), ledger.GetWalletRemainder(wallet));
            }
        }

        [Test]
        public void 財布を通らない計画は残数に触れず素材と返却だけを動かす()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var wallet = ForUnitTestModBlockId.GearBeltConveyor;
            var ledger = new BeltReplaceCostLedger(BuildWalletQuery(), Array.Empty<IItemStack>());

            new DirectCostBeltReplaceRemovalPlan(CreateRefund(1)).Commit(ledger);
            Assert.AreEqual(0, ledger.GetWalletRemainder(wallet));
            Assert.AreEqual(2, ledger.ArrivedRefunds.Count);

            // 返却1セットが入ったので1セット分の消費は賄える
            // One refunded set landed, so one set's worth of consumption is covered
            Assert.IsTrue(ledger.CanPay(CreateCostSets(1), Array.Empty<IItemStack>()));
            new DirectCostBeltReplacePlacementPlan(CreateCostSets(1)).Commit(ledger);
            Assert.IsFalse(ledger.CanPay(CreateCostSets(1), Array.Empty<IItemStack>()));
            Assert.AreEqual(0, ledger.GetWalletRemainder(wallet));
        }

        [Test]
        public void 仮定した返却は後続の支払い原資になるが届いた返却には数えない()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var wallet = ForUnitTestModBlockId.GearBeltConveyor;
            var ledger = new BeltReplaceCostLedger(BuildWalletQuery(), Array.Empty<IItemStack>());

            new AssumedFullRefundBeltReplaceRemovalPlan(CreateRefund(1), wallet).Commit(ledger);

            Assert.IsTrue(ledger.CanPay(CreateCostSets(1), Array.Empty<IItemStack>()));
            Assert.AreEqual(0, ledger.ArrivedRefunds.Count);

            // 全額返却を見込む＝凝縮した側を仮定するので残りは空へ戻る
            // Assuming the full refund means assuming the condensed side, so the remainder resets to empty
            Assert.AreEqual(ConstructionWalletUtil.AdvanceOnRemoval(0, true), ledger.GetWalletRemainder(wallet));
        }

        private static ConstructionWalletQuery BuildWalletQuery()
        {
            return new ConstructionWalletQuery(new ClientRemainingPlacementCountDatastore());
        }

        private static IReadOnlyList<(ItemId itemId, int count)> CreateCostSets(int setCount)
        {
            return new List<(ItemId, int)>
            {
                (MasterHolder.ItemMaster.GetItemId(Material1Guid), setCount),
                (MasterHolder.ItemMaster.GetItemId(Material2Guid), setCount),
            };
        }

        private static List<IItemStack> CreateRefund(int setCount)
        {
            var factory = ServerContext.ItemStackFactory;
            return new List<IItemStack>
            {
                factory.Create(MasterHolder.ItemMaster.GetItemId(Material1Guid), setCount),
                factory.Create(MasterHolder.ItemMaster.GetItemId(Material2Guid), setCount),
            };
        }
    }
}
