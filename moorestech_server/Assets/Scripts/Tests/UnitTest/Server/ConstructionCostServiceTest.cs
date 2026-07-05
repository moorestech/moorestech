using System;
using Core.Master;
using Game.Context;
using Game.PlayerInventory.Interface;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Server.Protocol.PacketResponse.Util.Construction;
using Tests.Module.TestMod;

namespace Tests.UnitTest.Server
{
    public class ConstructionCostServiceTest
    {
        private const int PlayerId = 1;

        private static readonly Guid Material1Guid = Guid.Parse("00000000-0000-0000-1234-000000000003"); // Test3
        private static readonly Guid Material2Guid = Guid.Parse("00000000-0000-0000-1234-000000000004"); // Test4

        // TestBlockのrequiredItems = Test3×2 + Test4×1 をコスト定義として使う
        // Use TestBlock's requiredItems (Test3 x2 + Test4 x1) as the cost definition

        [Test]
        public void 所持数が足りればHasRequiredItemsはtrue()
        {
            var serviceProvider = CreateServer();
            var inventory = GetInventory(serviceProvider);
            var requiredItems = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.BlockId).RequiredItems;

            inventory.SetItem(0, ServerContext.ItemStackFactory.Create(MasterHolder.ItemMaster.GetItemId(Material1Guid), 2));
            inventory.SetItem(1, ServerContext.ItemStackFactory.Create(MasterHolder.ItemMaster.GetItemId(Material2Guid), 1));

            Assert.IsTrue(ConstructionCostService.HasRequiredItems(requiredItems, inventory.InventoryItems));
        }

        [Test]
        public void 一部素材が不足していればHasRequiredItemsはfalse()
        {
            var serviceProvider = CreateServer();
            var inventory = GetInventory(serviceProvider);
            var requiredItems = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.BlockId).RequiredItems;

            inventory.SetItem(0, ServerContext.ItemStackFactory.Create(MasterHolder.ItemMaster.GetItemId(Material1Guid), 1));
            inventory.SetItem(1, ServerContext.ItemStackFactory.Create(MasterHolder.ItemMaster.GetItemId(Material2Guid), 1));

            Assert.IsFalse(ConstructionCostService.HasRequiredItems(requiredItems, inventory.InventoryItems));
        }

        [Test]
        public void ConsumeRequiredItemsは複数スロットにまたがって減算する()
        {
            var serviceProvider = CreateServer();
            var inventory = GetInventory(serviceProvider);
            var requiredItems = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.BlockId).RequiredItems;
            var material1Id = MasterHolder.ItemMaster.GetItemId(Material1Guid);
            var material2Id = MasterHolder.ItemMaster.GetItemId(Material2Guid);

            // 先頭スロットから消費確認
            // Split Test3 across two slots and verify consumption starts from the first slot
            inventory.SetItem(0, ServerContext.ItemStackFactory.Create(material1Id, 1));
            inventory.SetItem(5, ServerContext.ItemStackFactory.Create(material1Id, 3));
            inventory.SetItem(1, ServerContext.ItemStackFactory.Create(material2Id, 2));

            ConstructionCostService.ConsumeRequiredItems(requiredItems, inventory);

            Assert.AreEqual(0, inventory.GetItem(0).Count);
            Assert.AreEqual(2, inventory.GetItem(5).Count);
            Assert.AreEqual(1, inventory.GetItem(1).Count);
        }

        [Test]
        public void CreateRefundItemsはコスト全額のスタックを返す()
        {
            CreateServer();
            var requiredItems = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.BlockId).RequiredItems;

            var refundItems = ConstructionCostService.CreateRefundItems(requiredItems);

            Assert.AreEqual(2, refundItems.Count);
            Assert.AreEqual(MasterHolder.ItemMaster.GetItemId(Material1Guid), refundItems[0].Id);
            Assert.AreEqual(2, refundItems[0].Count);
            Assert.AreEqual(MasterHolder.ItemMaster.GetItemId(Material2Guid), refundItems[1].Id);
            Assert.AreEqual(1, refundItems[1].Count);
        }

        private static ServiceProvider CreateServer()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            return serviceProvider;
        }

        private static global::Core.Inventory.IOpenableInventory GetInventory(ServiceProvider serviceProvider)
        {
            return serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId).MainOpenableInventory;
        }
    }
}
