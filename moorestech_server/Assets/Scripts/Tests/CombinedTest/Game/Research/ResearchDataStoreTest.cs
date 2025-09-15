using System;
using System.Collections.Generic;
using System.Linq;
using Core.Master;
using Game.Context;
using Game.GameActionProcessor;
using Game.PlayerInventory.Interface;
using Game.Research;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;

namespace Tests.CombinedTest.Game.Research
{
    public class ResearchDataStoreTest
    {
        // もしインベントリのアイテムが足りないなら研究できない
        [Test]
        public void NotEnoughItemToFailResearchTest()
        {
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var researchDataStore = serviceProvider.GetService<IResearchDataStore>();
            var playerInventoryData = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(0);

            var researchGuid = Guid.Parse("cd05e30d-d599-46d3-a079-769113cbbf17"); // Research 1 - requires 1 item

            // アイテムがない状態で研究を試みる
            var result = researchDataStore.CompleteResearch(researchGuid, 0);

            Assert.IsFalse(result, "研究はアイテムが足りないため失敗するべき");
        }
        
        // 1つの前提研究が完了していないなら研究できない
        [Test]
        public void NotOneCompletedPreviousToFailResearchTest()
        {
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var researchDataStore = new ResearchDataStore(
                serviceProvider.GetService<IPlayerInventoryDataStore>(),
                serviceProvider.GetService<IGameActionExecutor>(),
                serviceProvider.GetService<ResearchEvent>()
            );
            var playerInventoryData = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(0);

            var researchGuid2 = Guid.Parse("7f1464a7-ba55-4b96-9257-cfdeddf5bbdd"); // Research 2 - requires Research 1
            var itemId1 = MasterHolder.ItemMaster.GetItemId(Guid.Parse("00000000-0000-0000-1234-000000000001"));

            // 必要なアイテムを追加
            var item = ServerContext.ItemStackFactory.Create(itemId1, 1);
            playerInventoryData.MainOpenableInventory.SetItem(0, item);

            // Research 1を完了せずにResearch 2を試みる
            var result = researchDataStore.CompleteResearch(researchGuid2, 0);

            Assert.IsFalse(result, "前提研究が完了していないため失敗するべき");
        }
        
        // 複数の前提研究が完了していないなら研究できない
        [Test]
        public void NotAllCompletedPreviousToFailResearchTest()
        {
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var researchDataStore = new ResearchDataStore(
                serviceProvider.GetService<IPlayerInventoryDataStore>(),
                serviceProvider.GetService<IGameActionExecutor>(),
                serviceProvider.GetService<ResearchEvent>()
            );
            var playerInventoryData = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(0);

            var researchGuid1 = Guid.Parse("cd05e30d-d599-46d3-a079-769113cbbf17"); // Research 1
            var researchGuid3 = Guid.Parse("d18ea842-7d03-42f1-ac80-29370083d040"); // Research 3 - requires Research 2
            var itemId1 = MasterHolder.ItemMaster.GetItemId(Guid.Parse("00000000-0000-0000-1234-000000000001"));
            var itemId2 = MasterHolder.ItemMaster.GetItemId(Guid.Parse("00000000-0000-0000-1234-000000000002"));

            // Research 1を完了させる
            var item = ServerContext.ItemStackFactory.Create(itemId1, 1);
            playerInventoryData.MainOpenableInventory.SetItem(0, item);
            researchDataStore.CompleteResearch(researchGuid1, 0);

            // Research 3に必要なアイテムを追加（Research 2は未完了）
            var item1 = ServerContext.ItemStackFactory.Create(itemId1, 2);
            var item2 = ServerContext.ItemStackFactory.Create(itemId2, 2);
            playerInventoryData.MainOpenableInventory.SetItem(1, item1);
            playerInventoryData.MainOpenableInventory.SetItem(2, item2);

            // Research 2を完了せずにResearch 3を試みる
            var result = researchDataStore.CompleteResearch(researchGuid3, 0);

            Assert.IsFalse(result, "すべての前提研究が完了していないため失敗するべき");
        }
        
        // すべての前提研究が完了しているなら研究できる
        [Test]
        public void AllCompletedPreviousToSuccessResearchTest()
        {
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var researchDataStore = new ResearchDataStore(
                serviceProvider.GetService<IPlayerInventoryDataStore>(),
                serviceProvider.GetService<IGameActionExecutor>(),
                serviceProvider.GetService<ResearchEvent>()
            );
            var playerInventoryData = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(0);

            var researchGuid1 = Guid.Parse("cd05e30d-d599-46d3-a079-769113cbbf17"); // Research 1
            var researchGuid2 = Guid.Parse("7f1464a7-ba55-4b96-9257-cfdeddf5bbdd"); // Research 2 - requires Research 1
            var itemId1 = MasterHolder.ItemMaster.GetItemId(Guid.Parse("00000000-0000-0000-1234-000000000001"));

            // Research 1を完了させる
            var item = ServerContext.ItemStackFactory.Create(itemId1, 1);
            playerInventoryData.MainOpenableInventory.SetItem(0, item);
            var result = researchDataStore.CompleteResearch(researchGuid1, 0);
            Assert.IsTrue(result, "Research 1の完了が成功するべき");

            // Research 2に必要なアイテムを追加
            item = ServerContext.ItemStackFactory.Create(itemId1, 1);
            playerInventoryData.MainOpenableInventory.SetItem(1, item);

            // Research 2を完了させる（Research 1は完了済み）
            result = researchDataStore.CompleteResearch(researchGuid2, 0);

            Assert.IsTrue(result, "すべての前提研究が完了しているため成功するべき");

            // アイテムが消費されたことを確認
            Assert.AreEqual(0, playerInventoryData.MainOpenableInventory.GetItem(1).Count);
        }
        
        
        
        
        // 保存、ロードテスト
        [Test]
        public void SaveLoadTest()
        {
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var researchDataStore = new ResearchDataStore(
                serviceProvider.GetService<IPlayerInventoryDataStore>(),
                serviceProvider.GetService<IGameActionExecutor>(),
                serviceProvider.GetService<ResearchEvent>()
            );
            var playerInventoryData = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(0);

            var researchGuid1 = Guid.Parse("cd05e30d-d599-46d3-a079-769113cbbf17"); // Research 1
            var researchGuid2 = Guid.Parse("7f1464a7-ba55-4b96-9257-cfdeddf5bbdd"); // Research 2
            var itemId1 = MasterHolder.ItemMaster.GetItemId(Guid.Parse("00000000-0000-0000-1234-000000000001"));

            // Research 1と2を完了させる
            var item = ServerContext.ItemStackFactory.Create(itemId1, 1);
            playerInventoryData.MainOpenableInventory.SetItem(0, item);
            researchDataStore.CompleteResearch(researchGuid1, 0);

            item = ServerContext.ItemStackFactory.Create(itemId1, 1);
            playerInventoryData.MainOpenableInventory.SetItem(1, item);
            researchDataStore.CompleteResearch(researchGuid2, 0);

            // セーブデータを取得
            var saveData = researchDataStore.GetSaveJsonObject();

            Assert.IsNotNull(saveData);
            Assert.IsNotNull(saveData.CompletedResearchGuids);
            Assert.AreEqual(2, saveData.CompletedResearchGuids.Count);
            Assert.Contains(researchGuid1.ToString(), saveData.CompletedResearchGuids.ToList());
            Assert.Contains(researchGuid2.ToString(), saveData.CompletedResearchGuids.ToList());

            // 新しいインスタンスを作成してロード
            var newResearchDataStore = new ResearchDataStore(
                serviceProvider.GetService<IPlayerInventoryDataStore>(),
                serviceProvider.GetService<IGameActionExecutor>(),
                serviceProvider.GetService<ResearchEvent>()
            );

            newResearchDataStore.LoadResearchData(saveData);

            // すでに完了した研究を再度完了できないことを確認
            item = ServerContext.ItemStackFactory.Create(itemId1, 1);
            playerInventoryData.MainOpenableInventory.SetItem(2, item);
            var result = newResearchDataStore.CompleteResearch(researchGuid1, 0);
            Assert.IsFalse(result, "すでに完了した研究は再度完了できないべき");

            // アイテムが消費されていないことを確認
            Assert.AreEqual(1, playerInventoryData.MainOpenableInventory.GetItem(2).Count);
        }

    }
}