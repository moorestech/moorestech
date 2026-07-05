using System;
using System.Linq;
using Core.Item;
using Game.Context;
using Game.PlayerInventory.Interface;
using Game.Research;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;

namespace Tests.CombinedTest.Game
{
    public class ItemStackLevelUpgradeTest
    {
        public const int PlayerId = 0;
        public static readonly Guid StackUpgradeResearchGuid = Guid.Parse("a5b6c7d8-0000-4000-8000-000000000001");
        public static readonly Guid Test1ItemGuid = Guid.Parse("00000000-0000-0000-1234-000000000001");

        // 研究完了で対象アイテムのスタック上限が上がる
        // Completing the research raises the target item's stack limit
        [Test]
        public void CompleteResearchUnlocksStackLevelTest()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var researchDataStore = serviceProvider.GetService<IResearchDataStore>();

            Assert.AreEqual(100, ItemStackLevelDataStore.Instance.GetMaxStack(ForUnitTestItemId.ItemId1));

            var result = researchDataStore.CompleteResearch(StackUpgradeResearchGuid, PlayerId);
            Assert.IsTrue(result);
            Assert.AreEqual(200, ItemStackLevelDataStore.Instance.GetMaxStack(ForUnitTestItemId.ItemId1));
        }

        // アップグレード後は基礎上限を超えて挿入・合算できる
        // After the upgrade, more than the base limit can be inserted and merged
        [Test]
        public void UpgradedItemCanStackBeyondBaseLimitTest()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var researchDataStore = serviceProvider.GetService<IResearchDataStore>();
            researchDataStore.CompleteResearch(StackUpgradeResearchGuid, PlayerId);

            var inventory = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId);
            var item150 = ServerContext.ItemStackFactory.Create(Test1ItemGuid, 150);
            inventory.MainOpenableInventory.InsertItem(item150);

            // 強化後は150個が1スロットに収まりあふれない（メインInvはホットバー優先挿入）
            // After the upgrade 150 fits in a single slot without overflow (main inv inserts hotbar-first)
            var occupiedSlots = inventory.MainOpenableInventory.InventoryItems.Where(item => item.Count > 0).ToList();
            Assert.AreEqual(1, occupiedSlots.Count);
            Assert.AreEqual(150, occupiedSlots[0].Count);
        }

        // アップグレード前は従来通り100であふれる
        // Before the upgrade, stacks still overflow at 100
        [Test]
        public void NonUpgradedItemStillOverflowsAtBaseLimitTest()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            var inventory = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId);
            var item100A = ServerContext.ItemStackFactory.Create(Test1ItemGuid, 100);
            var item50 = ServerContext.ItemStackFactory.Create(Test1ItemGuid, 50);
            inventory.MainOpenableInventory.InsertItem(item100A);
            inventory.MainOpenableInventory.InsertItem(item50);

            // 強化前は上限100であふれ、100と50の2スタックに分かれる
            // Before the upgrade it overflows at 100, splitting into two stacks of 100 and 50
            var occupiedCounts = inventory.MainOpenableInventory.InventoryItems.Where(item => item.Count > 0).Select(item => item.Count).ToList();
            Assert.AreEqual(2, occupiedCounts.Count);
            Assert.AreEqual(100, occupiedCounts[0]);
            Assert.AreEqual(50, occupiedCounts[1]);
        }
    }
}
