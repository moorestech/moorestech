using System;
using System.Linq;
using Core.Item;
using Game.Context;
using Game.PlayerInventory.Interface;
using Game.Research;
using Game.SaveLoad.Interface;
using Game.SaveLoad.Json;
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
            var occupiedSlots = inventory.MainOpenableInventory.InventoryItems.Where(item => 0 < item.Count).ToList();
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
            var occupiedCounts = inventory.MainOpenableInventory.InventoryItems.Where(item => 0 < item.Count).Select(item => item.Count).ToList();
            Assert.AreEqual(2, occupiedCounts.Count);
            Assert.AreEqual(100, occupiedCounts[0]);
            Assert.AreEqual(50, occupiedCounts[1]);
        }

        // 強化後の個数を持つセーブが正常にロードできる（ロード順回帰）
        // A save containing counts above the base limit loads without errors (load-order regression)
        [Test]
        public void SaveWithUpgradedStackLoadsSuccessfullyTest()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var researchDataStore = serviceProvider.GetService<IResearchDataStore>();
            researchDataStore.CompleteResearch(StackUpgradeResearchGuid, PlayerId);

            // 基礎上限100を超える150個を1スロットに保持した状態でセーブ
            // Save with a single slot holding 150 items, above the base limit of 100
            var inventory = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId);
            inventory.MainOpenableInventory.InsertItem(ServerContext.ItemStackFactory.Create(Test1ItemGuid, 150));
            var saveJson = serviceProvider.GetService<AssembleSaveJsonText>().AssembleSaveJson();

            // 新しいコンテナでロード。レベル復元がインベントリ復元より先でなければ例外死する
            // Load in a fresh container; this throws unless levels are restored before the inventory
            var (_, loadServiceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            (loadServiceProvider.GetService<IWorldSaveDataLoader>() as WorldLoaderFromJson).Load(saveJson);

            // 150個が1スタックで復元される（メインInvはホットバー優先挿入のため非空スロットで検証）
            // 150 items are restored as a single stack (verified by non-empty slot due to hotbar-first insert)
            var loadedInventory = loadServiceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId);
            var occupiedSlots = loadedInventory.MainOpenableInventory.InventoryItems.Where(item => 0 < item.Count).ToList();
            Assert.AreEqual(1, occupiedSlots.Count);
            Assert.AreEqual(150, occupiedSlots[0].Count);
            Assert.AreEqual(2, ItemStackLevelDataStore.Instance.GetUnlockedLevel(Test1ItemGuid));
        }

        // ロード後の研究再実行と永続化レベルが二重適用されない
        // Research re-execution after load does not double-apply on top of persisted levels
        [Test]
        public void LoadDoesNotDoubleApplyLevelsTest()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            serviceProvider.GetService<IResearchDataStore>().CompleteResearch(StackUpgradeResearchGuid, PlayerId);
            var saveJson = serviceProvider.GetService<AssembleSaveJsonText>().AssembleSaveJson();

            var (_, loadServiceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            (loadServiceProvider.GetService<IWorldSaveDataLoader>() as WorldLoaderFromJson).Load(saveJson);

            // 冪等 unlock（max適用）なのでレベルは2のまま
            // Idempotent unlock (max-based) keeps the level at exactly 2
            Assert.AreEqual(2, ItemStackLevelDataStore.Instance.GetUnlockedLevel(Test1ItemGuid));
        }
    }
}
