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
