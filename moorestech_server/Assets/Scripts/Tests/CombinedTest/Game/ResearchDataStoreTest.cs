using System;
using Core.Master;
using Game.Context;
using Game.PlayerInventory.Interface;
using Game.Research;
using Game.SaveLoad.Interface;
using Game.SaveLoad.Json;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using Game.UnlockState;
using Tests.Util;

namespace Tests.CombinedTest.Game
{
    public class ResearchDataStoreTest
    {
        public const int PlayerId = 0;
        
        public static readonly Guid Research1Guid = Guid.Parse("cd05e30d-d599-46d3-a079-769113cbbf17");
        public static readonly Guid Research2Guid = Guid.Parse("7f1464a7-ba55-4b96-9257-cfdeddf5bbdd");
        public static readonly Guid Research3Guid = Guid.Parse("d18ea842-7d03-42f1-ac80-29370083d040");
        public static readonly Guid Research4Guid = Guid.Parse("bf9bda9e-dace-43c4-9a33-75f248fd17f6");
        public static readonly Guid Test2ItemGuid = Guid.Parse("00000000-0000-0000-1234-000000000002");
        public static readonly Guid Test3ItemGuid = Guid.Parse("00000000-0000-0000-1234-000000000003");
        public static readonly Guid CraftRecipeGuid = Guid.Parse("00000010-0000-0000-0000-000000000000");
        
        // もしインベントリのアイテムが足りないなら研究できない
        // If you don't have enough inventory items, you can't research them.
        [Test]
        public void NotEnoughItemToFailResearchTest()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var researchDataStore = serviceProvider.GetService<IResearchDataStore>();
            
            // アイテムがない状態で研究を試みる
            // Attempting research without the item
            var result = researchDataStore.CompleteResearch(Research1Guid, PlayerId);

            Assert.IsFalse(result);
        }
        
        // 1つの前提研究が完了していないなら研究できない
        // If one prerequisite study is not completed, you cannot research it.
        [Test]
        public void NotOneCompletedPreviousToFailResearchTest()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var researchDataStore = serviceProvider.GetService<IResearchDataStore>();
            var playerInventoryData = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId);
            
            // 必要なアイテムを追加
            // Add necessary items
            var researchMaster = MasterHolder.ResearchMaster.GetResearch(Research2Guid);
            foreach (var consumeItem in researchMaster.ConsumeItems)
            {
                var item = ServerContext.ItemStackFactory.Create(consumeItem.ItemGuid, consumeItem.ItemCount);
                playerInventoryData.MainOpenableInventory.InsertItem(item);
            }

            // Research 1を完了せずにResearch 2を試みる
            // Try Research 2 without completing Research 1
            var result = researchDataStore.CompleteResearch(researchMaster.ResearchNodeGuid, PlayerId);

            Assert.IsFalse(result);
        }
        
        // 複数の前提研究が完了していないなら研究できない
        [Test]
        public void NotAllCompletedPreviousToFailResearchTest()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var researchDataStore = serviceProvider.GetService<IResearchDataStore>();
            var playerInventoryData = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId);

            // Research 1を完了させる
            // Complete Research 1
            CompleteResearchForTest(serviceProvider, Research1Guid);
            
            // Research 3に必要なアイテムを追加（Research 2は未完了）
            // Add necessary items for Research 3 (Research 2 is incomplete)
            var researchMaster = MasterHolder.ResearchMaster.GetResearch(Research3Guid);
            foreach (var consumeItem in researchMaster.ConsumeItems)
            {
                var item = ServerContext.ItemStackFactory.Create(consumeItem.ItemGuid, consumeItem.ItemCount);
                playerInventoryData.MainOpenableInventory.InsertItem(item);
            }

            // Research 2を完了せずにResearch 3を試みる
            var result = researchDataStore.CompleteResearch(Research3Guid, 0);

            Assert.IsFalse(result, "すべての前提研究が完了していないため失敗するべき");
        }
        
        // すべての前提研究が完了しているなら研究できる
        // If all prerequisite studies are completed, you can research them.
        [Test]
        public void AllCompletedPreviousToSuccessResearchTest()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            
            // Research 1, 2を完了させる
            // Complete Research 1 and 2
            CompleteResearchForTest(serviceProvider, Research1Guid);
            CompleteResearchForTest(serviceProvider, Research2Guid);
            
            // Research 3に必要なアイテムを追加
            // Add necessary items for Research 3
            var playerInventoryData = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId);
            var researchMaster = MasterHolder.ResearchMaster.GetResearch(Research3Guid);
            foreach (var consumeItem in researchMaster.ConsumeItems)
            {
                var item = ServerContext.ItemStackFactory.Create(consumeItem.ItemGuid, consumeItem.ItemCount);
                playerInventoryData.MainOpenableInventory.InsertItem(item);
            }
            
            var researchDataStore = serviceProvider.GetService<IResearchDataStore>();
            var result = researchDataStore.CompleteResearch(Research3Guid, PlayerId);
            Assert.IsTrue(result);
        }

        [Test]
        public void Research4GiveItemRewardTest()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            
            // Research 1を完了させる
            // Complete Research 1
            CompleteResearchForTest(serviceProvider, Research4Guid);

            // giveItem アクションにより研究完了者のインベントリに報酬が入ることを確認
            // Confirm that the reward is added to the inventory of the researcher by the giveItem action
            var rewardCount = PlayerInventoryUtil.GetInInventoryItemCount(serviceProvider, PlayerId, Test2ItemGuid);
            Assert.AreEqual(3, rewardCount, "giveItem アクションにより研究完了者のインベントリに報酬が入る");
        }
        
        // 前提研究が無いなら研究できる
        // If there is no prerequisite research, you can research it.
        [Test]
        public void NoPreviousToSuccessResearchTest()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            
            CompleteResearchForTest(serviceProvider, Research4Guid);
        }

        [Test]
        public void CompleteResearch4UnlocksTest3ItemAndCraftRecipe()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            var unlockState = serviceProvider.GetService<IGameUnlockStateDataController>();
            var test3ItemId = MasterHolder.ItemMaster.GetItemId(Test3ItemGuid);

            Assert.IsFalse(unlockState.ItemUnlockStateInfos[test3ItemId].IsUnlocked, "Research4 completion前にTest3アイテムはロックされている想定");

            CompleteResearchForTest(serviceProvider, Research4Guid);

            Assert.IsTrue(unlockState.ItemUnlockStateInfos[test3ItemId].IsUnlocked, "Research4 completion後にTest3アイテムがアンロックされるべき");
            Assert.IsTrue(unlockState.CraftRecipeUnlockStateInfos[CraftRecipeGuid].IsUnlocked, "Research4 completion後にクラフトレシピがアンロックされるべき");
        }
        
        
        public static void CompleteResearchForTest(ServiceProvider serviceProvider, Guid researchGuid)
        {
            CompleteResearchForTest(serviceProvider, researchGuid, PlayerId);
        }

        public static void CompleteResearchForTest(ServiceProvider serviceProvider, Guid researchGuid, int playerId)
        {
            // 必要なサービスを取得
            // Get necessary services
            var researchDataStore = serviceProvider.GetService<IResearchDataStore>();
            var playerInventoryData = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(playerId);
            
            // 研究完了に必要なアイテムを追加
            // Add items required to complete the research
            var researchMaster = MasterHolder.ResearchMaster.GetResearch(researchGuid);
            foreach (var consumeItem in researchMaster.ConsumeItems)
            {
                var item = ServerContext.ItemStackFactory.Create(consumeItem.ItemGuid, consumeItem.ItemCount);
                playerInventoryData.MainOpenableInventory.InsertItem(item);
            }

            // 研究を完了させる
            // Complete the research
            var result = researchDataStore.CompleteResearch(researchGuid, playerId);
            Assert.IsTrue(result);
        }
    }
}
