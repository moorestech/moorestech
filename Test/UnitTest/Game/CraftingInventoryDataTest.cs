#if NET6_0
using Core.Item;
using Core.Item.Config;
using Game.Crafting.Interface;
using Game.PlayerInventory.Interface;
using Game.PlayerInventory.Interface.Event;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using PlayerInventory.Event;
using PlayerInventory.ItemManaged;
using Server.Boot;
using Test.Module.TestMod;

namespace Test.UnitTest.Game
{
    /// <summary>
    ///     
    /// </summary>
    public class CraftingInventoryDataTest
    {
        private const int PlayerId = 0;

        private const int NormalCraftConfig = 0;
        private const int RemainItemCraftConfig = 3;

        [Test]
        public void GetCreatableItemTest()
        {
            var (_, serviceProvider) = new PacketResponseCreatorDiContainerGenerators().Create(TestModDirectory.ForUnitTestModDirectory);
            var itemStackFactory = serviceProvider.GetService<ItemStackFactory>();
            var config = serviceProvider.GetService<ICraftingConfig>();
            var service = serviceProvider.GetService<IIsCreatableJudgementService>();

            var craftConfig = config.GetCraftingConfigList()[NormalCraftConfig];

            //craftingInventory
            var main = new MainOpenableInventoryData(PlayerId, new MainInventoryUpdateEvent(), itemStackFactory);
            var grab = new GrabInventoryData(PlayerId, new GrabInventoryUpdateEvent(), itemStackFactory);
            var craftingInventory = new CraftingOpenableInventoryData(PlayerId, new CraftInventoryUpdateEvent(), itemStackFactory, service, main, grab, new CraftingEvent());
            for (var i = 0; i < craftConfig.CraftItemInfos.Count; i++) craftingInventory.SetItem(i, craftConfig.CraftItemInfos[i].ItemStack);

            //config
            //getCreatableItem
            Assert.AreEqual(craftConfig.Result, craftingInventory.GetCreatableItem());
        }

        
        [Test]
        public void CraftTest()
        {
            var (_, serviceProvider) = new PacketResponseCreatorDiContainerGenerators().Create(TestModDirectory.ForUnitTestModDirectory);
            var itemStackFactory = serviceProvider.GetService<ItemStackFactory>();
            var config = serviceProvider.GetService<ICraftingConfig>();
            var service = serviceProvider.GetService<IIsCreatableJudgementService>();
            var craftingEvent = serviceProvider.GetService<ICraftingEvent>();

            var main = new MainOpenableInventoryData(PlayerId, new MainInventoryUpdateEvent(), itemStackFactory);
            var grabInventory = new GrabInventoryData(PlayerId, new GrabInventoryUpdateEvent(), itemStackFactory);


            var craftConfig = config.GetCraftingConfigList()[NormalCraftConfig];


            //craftingInventory
            var craftingInventory = new CraftingOpenableInventoryData(PlayerId, new CraftInventoryUpdateEvent(), itemStackFactory, service, main, grabInventory, (CraftingEvent)craftingEvent);
            for (var i = 0; i < craftConfig.CraftItemInfos.Count; i++) craftingInventory.SetItem(i, craftConfig.CraftItemInfos[i].ItemStack);

            
            craftingInventory.NormalCraft();

            //grabInventory
            Assert.AreEqual(craftConfig.Result, grabInventory.GetItem(0));

            
            for (var i = 0; i < PlayerInventoryConst.CraftingSlotSize; i++) Assert.AreEqual(itemStackFactory.CreatEmpty(), craftingInventory.GetItem(i));
        }

        
        [Test]
        public void CraftRemainItemTest()
        {
            var (_, serviceProvider) = new PacketResponseCreatorDiContainerGenerators().Create(TestModDirectory.ForUnitTestModDirectory);
            var itemStackFactory = serviceProvider.GetService<ItemStackFactory>();
            var config = serviceProvider.GetService<ICraftingConfig>();
            var service = serviceProvider.GetService<IIsCreatableJudgementService>();
            var craftingEvent = serviceProvider.GetService<ICraftingEvent>();

            var main = new MainOpenableInventoryData(PlayerId, new MainInventoryUpdateEvent(), itemStackFactory);
            var grabInventory = new GrabInventoryData(PlayerId, new GrabInventoryUpdateEvent(), itemStackFactory);


            var craftConfig = config.GetCraftingConfigList()[RemainItemCraftConfig];


            //craftingInventory
            var craftingInventory = new CraftingOpenableInventoryData(PlayerId, new CraftInventoryUpdateEvent(), itemStackFactory, service, main, grabInventory, (CraftingEvent)craftingEvent);
            for (var i = 0; i < craftConfig.CraftItemInfos.Count; i++) craftingInventory.SetItem(i, craftConfig.CraftItemInfos[i].ItemStack);

            
            craftingInventory.NormalCraft();

            //grabInventory
            Assert.AreEqual(craftConfig.Result, grabInventory.GetItem(0));

            
            for (var i = 0; i < PlayerInventoryConst.CraftingSlotSize; i++)
                if (craftConfig.CraftItemInfos[i].IsRemain)
                    Assert.AreEqual(craftConfig.CraftItemInfos[i].ItemStack, craftingInventory.GetItem(i));
                else
                    Assert.AreEqual(itemStackFactory.CreatEmpty(), craftingInventory.GetItem(i));
        }


        
        [Test]
        public void CraftRemainderItemTest()
        {
            var (_, serviceProvider) = new PacketResponseCreatorDiContainerGenerators().Create(TestModDirectory.ForUnitTestModDirectory);
            var itemStackFactory = serviceProvider.GetService<ItemStackFactory>();
            var config = serviceProvider.GetService<ICraftingConfig>();
            var service = serviceProvider.GetService<IIsCreatableJudgementService>();

            var main = new MainOpenableInventoryData(PlayerId, new MainInventoryUpdateEvent(), itemStackFactory);
            var grabInventory = new GrabInventoryData(PlayerId, new GrabInventoryUpdateEvent(), itemStackFactory);

            var craftConfig = config.GetCraftingConfigList()[NormalCraftConfig];


            //craftingInventory1
            var craftingInventory = new CraftingOpenableInventoryData(PlayerId, new CraftInventoryUpdateEvent(), itemStackFactory, service, main, grabInventory, new CraftingEvent());
            for (var i = 0; i < craftConfig.CraftItemInfos.Count; i++)
            {
                var itemId = craftConfig.CraftItemInfos[i].ItemStack.Id;
                var itemCount = craftConfig.CraftItemInfos[i].ItemStack.Count;
                var setItem = itemStackFactory.Create(itemId, itemCount + 1);
                craftingInventory.SetItem(i, setItem);
            }

            
            craftingInventory.NormalCraft();

            //grabInventory
            Assert.AreEqual(craftConfig.Result, grabInventory.GetItem(0));

            //1
            for (var i = 0; i < PlayerInventoryConst.CraftingSlotSize; i++)
            {
                var inventoryItem = craftingInventory.GetItem(i);

                Assert.AreEqual(craftConfig.CraftItemInfos[i].ItemStack.Id, inventoryItem.Id); //check id
                
                if (craftConfig.CraftItemInfos[i].ItemStack.Count != 0) Assert.AreEqual(1, inventoryItem.Count); //check count
            }
        }

        [Test]
        
        public void NoneCraftSlotItemTest()
        {
            var (_, serviceProvider) = new PacketResponseCreatorDiContainerGenerators().Create(TestModDirectory.ForUnitTestModDirectory);
            var itemStackFactory = serviceProvider.GetService<ItemStackFactory>();
            var config = serviceProvider.GetService<ICraftingConfig>();
            var service = serviceProvider.GetService<IIsCreatableJudgementService>();

            var main = new MainOpenableInventoryData(PlayerId, new MainInventoryUpdateEvent(), itemStackFactory);
            var grabInventory = new GrabInventoryData(PlayerId, new GrabInventoryUpdateEvent(), itemStackFactory);


            var craftingInventory = new CraftingOpenableInventoryData(PlayerId, new CraftInventoryUpdateEvent(), itemStackFactory, service, main, grabInventory, new CraftingEvent());


            //grabInventory
            craftingInventory.NormalCraft();
            Assert.AreEqual(itemStackFactory.CreatEmpty(), grabInventory.GetItem(0));
        }


        
        [Test]
        public void CanNotInsertOutputSlotToCanNotCraftTest()
        {
            var (_, serviceProvider) = new PacketResponseCreatorDiContainerGenerators().Create(TestModDirectory.ForUnitTestModDirectory);
            var itemStackFactory = serviceProvider.GetService<ItemStackFactory>();
            var config = serviceProvider.GetService<ICraftingConfig>();
            var itemConfig = serviceProvider.GetService<IItemConfig>();
            var service = serviceProvider.GetService<IIsCreatableJudgementService>();

            var main = new MainOpenableInventoryData(PlayerId, new MainInventoryUpdateEvent(), itemStackFactory);
            var grabInventory = new GrabInventoryData(PlayerId, new GrabInventoryUpdateEvent(), itemStackFactory);


            var craftConfig = config.GetCraftingConfigList()[NormalCraftConfig];
            var resultId = craftConfig.Result.Id;


            //craftingInventory
            var craftingInventory = new CraftingOpenableInventoryData(PlayerId, new CraftInventoryUpdateEvent(), itemStackFactory, service, main, grabInventory, new CraftingEvent());
            for (var i = 0; i < craftConfig.CraftItemInfos.Count; i++) craftingInventory.SetItem(i, craftConfig.CraftItemInfos[i].ItemStack);


            
            
            var setItem = itemStackFactory.Create(resultId + 1, 1);
            grabInventory.SetItem(0, setItem);

            
            craftingInventory.NormalCraft();

            
            Assert.AreEqual(setItem, grabInventory.GetItem(0));
            
            for (var i = 0; i < craftConfig.CraftItemInfos.Count; i++) Assert.AreEqual(craftConfig.CraftItemInfos[i].ItemStack, craftingInventory.GetItem(i));


            
            
            setItem = itemStackFactory.Create(resultId, itemConfig.GetItemConfig(resultId).MaxStack);
            grabInventory.SetItem(0, setItem);

            
            craftingInventory.NormalCraft();

            
            Assert.AreEqual(setItem, grabInventory.GetItem(0));
            
            for (var i = 0; i < craftConfig.CraftItemInfos.Count; i++) Assert.AreEqual(craftConfig.CraftItemInfos[i].ItemStack, craftingInventory.GetItem(i));
        }
    }
}
#endif