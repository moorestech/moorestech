using System;
using System.Collections.Generic;
using Core.Inventory;
using Core.Master;
using Game.Action;
using Game.PlayerInventory.Interface;
using Game.Research;
using Game.Research.Interface;
using Mooresmaster.Model.ChallengeActionModule;
using Mooresmaster.Model.ResearchModule;
using NUnit.Framework;
using Tests.Module.TestMod;
using Newtonsoft.Json.Linq;

namespace Tests.CombinedTest.Game.Research
{
    [TestFixture]
    public class ResearchDataStoreTest
    {
        private ResearchDataStore _researchDataStore;
        private TestPlayerInventoryDataStore _inventoryDataStore;
        private TestGameActionExecutor _gameActionExecutor;
        private ResearchEvent _researchEvent;
        private ResearchNodeMasterElement _testResearch;
        private ResearchNodeMasterElement _testResearch2;

        [SetUp]
        public void SetUp()
        {
            _inventoryDataStore = new TestPlayerInventoryDataStore();
            _gameActionExecutor = new TestGameActionExecutor();
            _researchEvent = new ResearchEvent();
            _researchDataStore = new ResearchDataStore(_inventoryDataStore, _gameActionExecutor, _researchEvent);

            // テスト用の研究データを設定
            _testResearch = new ResearchNodeMasterElement
            {
                ResearchNodeGuid = Guid.NewGuid(),
                ResearchNodetName = "Test Research 1",
                ResearchNodeDescription = "Test Description",
                ClearedActions = new ChallengeActionElement[]
                {
                    new ChallengeActionElement
                    {
                        ChallengeActionType = ChallengeActionElement.ChallengeActionTypeConst.unlockCraftRecipe,
                        UnlockIds = new List<string> { "test_recipe" }
                    }
                },
                PrevResearchNodeGuid = Guid.Empty
            };

            _testResearch2 = new ResearchNodeMasterElement
            {
                ResearchNodeGuid = Guid.NewGuid(),
                ResearchNodetName = "Test Research 2",
                ResearchNodeDescription = "Test Description 2",
                ClearedActions = new ChallengeActionElement[0],
                PrevResearchNodeGuid = _testResearch.ResearchNodeGuid
            };

            // ResearchMasterのモックを設定
            var testResearchMaster = new TestResearchMaster();
            testResearchMaster.AddResearch(_testResearch);
            testResearchMaster.AddResearch(_testResearch2);
            MasterHolder.ResearchMaster = testResearchMaster;

            // ItemMasterのモックを設定
            var testItemMaster = new TestItemMaster();
            MasterHolder.ItemMaster = testItemMaster;
        }

        [Test]
        public void IsResearchCompleted_未完了の研究_Falseを返す()
        {
            // Act
            var result = _researchDataStore.IsResearchCompleted(_testResearch.ResearchNodeGuid);

            // Assert
            Assert.IsFalse(result);
        }

        [Test]
        public void CompleteResearch_正常な完了_成功()
        {
            // Arrange
            var playerId = 1;
            _inventoryDataStore.SetupPlayerInventory(playerId, VanillaItemGuid.TestItem1Guid, 10);

            // Act - ConsumeItemsがない研究として扱う（現時点では）
            var result = _researchDataStore.CompleteResearch(_testResearch.ResearchNodeGuid, playerId);

            // Assert
            Assert.IsTrue(result.Success);
            Assert.AreEqual(_testResearch.ResearchNodeGuid, result.CompletedResearchGuid);
            Assert.IsTrue(_researchDataStore.IsResearchCompleted(_testResearch.ResearchNodeGuid));
            Assert.AreEqual(1, _gameActionExecutor.ExecutedActions.Count);
        }

        [Test]
        public void CompleteResearch_前提研究未完了_失敗()
        {
            // Arrange
            var playerId = 1;

            // Act
            var result = _researchDataStore.CompleteResearch(_testResearch2.ResearchNodeGuid, playerId);

            // Assert
            Assert.IsFalse(result.Success);
            Assert.IsFalse(_researchDataStore.IsResearchCompleted(_testResearch2.ResearchNodeGuid));
        }

        [Test]
        public void CompleteResearch_前提研究完了後_成功()
        {
            // Arrange
            var playerId = 1;
            _inventoryDataStore.SetupPlayerInventory(playerId, VanillaItemGuid.TestItem1Guid, 10);

            // 前提研究を完了
            _researchDataStore.CompleteResearch(_testResearch.ResearchNodeGuid, playerId);

            // Act
            var result = _researchDataStore.CompleteResearch(_testResearch2.ResearchNodeGuid, playerId);

            // Assert
            Assert.IsTrue(result.Success);
            Assert.IsTrue(_researchDataStore.IsResearchCompleted(_testResearch2.ResearchNodeGuid));
        }

        [Test]
        public void SaveLoad_研究状態が保存される()
        {
            // Arrange
            var playerId = 1;
            _inventoryDataStore.SetupPlayerInventory(playerId, VanillaItemGuid.TestItem1Guid, 10);
            _researchDataStore.CompleteResearch(_testResearch.ResearchNodeGuid, playerId);

            // Act
            var saveData = _researchDataStore.GetSaveJsonObject();
            var newDataStore = new ResearchDataStore(_inventoryDataStore, _gameActionExecutor, _researchEvent);
            newDataStore.LoadResearchData(saveData);

            // Assert
            Assert.IsTrue(newDataStore.IsResearchCompleted(_testResearch.ResearchNodeGuid));
            Assert.AreEqual(1, newDataStore.GetCompletedResearchGuids().Count);
        }

        private class TestPlayerInventoryDataStore : IPlayerInventoryDataStore
        {
            private readonly Dictionary<int, TestPlayerInventoryData> _inventories = new();

            public void SetupPlayerInventory(int playerId, Guid itemGuid, int count)
            {
                var inventory = new TestPlayerInventoryData();
                var itemId = MasterHolder.ItemMaster.GetItemId(itemGuid);
                inventory.MainOpenableInventory.SetItem(0, new Core.Item.ItemStack(itemId, count));
                _inventories[playerId] = inventory;
            }

            public IPlayerInventoryData GetInventoryData(int playerId)
            {
                return _inventories.TryGetValue(playerId, out var inventory) ? inventory : null;
            }

            public List<IPlayerInventoryData> GetAllInventoryData()
            {
                return new List<IPlayerInventoryData>(_inventories.Values);
            }
        }

        private class TestPlayerInventoryData : IPlayerInventoryData
        {
            public IOpenableInventory MainOpenableInventory { get; }
            public IOpenableInventory GrabInventory { get; }
            public int PlayerId { get; }

            public TestPlayerInventoryData()
            {
                MainOpenableInventory = new TestOpenableInventory();
                GrabInventory = new TestOpenableInventory();
                PlayerId = 1;
            }
        }

        private class TestOpenableInventory : IOpenableInventory
        {
            public List<Core.Item.IItemStack> InventoryItems { get; } = new();
            public int GetSlotSize() => 36;

            public TestOpenableInventory()
            {
                for (int i = 0; i < GetSlotSize(); i++)
                {
                    InventoryItems.Add(Core.Item.ItemStackFactory.Create(ItemConstant.EmptyItemId, 0));
                }
            }

            public void SetItem(int slot, Core.Item.IItemStack itemStack)
            {
                if (slot >= 0 && slot < InventoryItems.Count)
                {
                    InventoryItems[slot] = itemStack;
                }
            }

            public Core.Item.IItemStack InsertItem(Core.Item.IItemStack itemStack)
            {
                return itemStack;
            }

            public List<Core.Item.IItemStack> InsertItem(List<Core.Item.IItemStack> itemStacks)
            {
                return itemStacks;
            }

            public bool IsSlotEmpty(int slot) => InventoryItems[slot].Id.Equals(ItemConstant.EmptyItemId);

            public void NormalizeSlot(Core.Master.MasterHolder masterHolder) { }

            public Core.Item.IItemStack ReplaceItem(int slot, Core.Item.IItemStack itemStack)
            {
                var old = InventoryItems[slot];
                InventoryItems[slot] = itemStack;
                return old;
            }

            public (Core.Item.IItemStack, int) GetItem(int slot)
            {
                return (InventoryItems[slot], slot);
            }

            public bool TryGetItem(int slot, out (Core.Item.IItemStack, int) itemStackAndSlot)
            {
                itemStackAndSlot = (InventoryItems[slot], slot);
                return true;
            }
        }

        private class TestResearchMaster : ResearchMaster
        {
            private readonly Dictionary<Guid, ResearchNodeMasterElement> _researches = new();

            public TestResearchMaster() : base(JToken.Parse("{}"))
            {
            }

            public void AddResearch(ResearchNodeMasterElement research)
            {
                _researches[research.ResearchNodeGuid] = research;
            }

            public new ResearchNodeMasterElement GetResearch(Guid researchGuid)
            {
                return _researches.TryGetValue(researchGuid, out var research) ? research : null;
            }
        }

        private class TestItemMaster : ItemMaster
        {
            public TestItemMaster() : base(JToken.Parse("{\"data\":[]}"))
            {
            }

            public new ItemId GetItemId(Guid itemGuid)
            {
                return new ItemId(1);
            }
        }
    }
}