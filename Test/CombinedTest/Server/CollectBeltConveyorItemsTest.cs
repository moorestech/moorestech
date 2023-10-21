#if NET6_0
using System;
using System.Collections.Generic;
using System.Reflection;
using Core.Item;
using Core.Update;
using Game.Block.Blocks.BeltConveyor;
using Game.Block.Interface.BlockConfig;
using Game.Entity.Interface;
using Game.World.Interface.DataStore;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Server.Protocol.PacketResponse.Const;
using Server.Protocol.PacketResponse.Util;
using Test.Module.TestMod;

namespace Test.CombinedTest.Server
{
    public class CollectBeltConveyorItemsTest
    {
        //41
        private const int TimeOfItemEnterToExit = 4000;
        private const int RemainingTime = 1000;
        private const int ItemInstanceId = 100;
        private readonly List<Coordinate> _minusPlayerCoordinate = new() { new Coordinate(-ChunkResponseConst.ChunkSize, -ChunkResponseConst.ChunkSize) };
        private readonly List<Coordinate> _plusPlayerCoordinate = new() { new Coordinate(0, 0) };


        ///     

        [Test]
        public void BlockDirectionItemPositionTest()
        {
            var (packet, serviceProvider) = new PacketResponseCreatorDiContainerGenerators().Create(TestModDirectory.ForUnitTestModDirectory);
            var itemsStackFactory = serviceProvider.GetService<ItemStackFactory>();
            var worldDataStore = serviceProvider.GetService<IWorldBlockDatastore>();
            var blockConfig = serviceProvider.GetService<IBlockConfig>();
            var entityFactory = serviceProvider.GetService<IEntityFactory>();


            //x,y 

            
            worldDataStore.AddBlock(CreateOneItemInsertedItem(itemsStackFactory), 0, 0, BlockDirection.North);

            
            var itemEntity = CollectBeltConveyorItems.CollectItem(_plusPlayerCoordinate, worldDataStore, blockConfig, entityFactory)[0];
            
            Assert.AreEqual(0.5, itemEntity.Position.X); //0,0Z0.5 
            Assert.AreEqual(0, itemEntity.Position.Y); //23Y0
            Assert.AreEqual(0.75, itemEntity.Position.Z); //4113/4
            
            Assert.AreEqual(ItemInstanceId, itemEntity.InstanceId);
            Assert.AreEqual(VanillaEntityType.VanillaItem, itemEntity.EntityType);


            
            worldDataStore.RemoveBlock(0, 0);
            worldDataStore.AddBlock(CreateOneItemInsertedItem(itemsStackFactory), 0, 0, BlockDirection.East);
            itemEntity = CollectBeltConveyorItems.CollectItem(_plusPlayerCoordinate, worldDataStore, blockConfig, entityFactory)[0];
            
            Assert.AreEqual(0.75, itemEntity.Position.X);
            Assert.AreEqual(0, itemEntity.Position.Y);
            Assert.AreEqual(0.5, itemEntity.Position.Z);


            
            worldDataStore.RemoveBlock(0, 0);
            worldDataStore.AddBlock(CreateOneItemInsertedItem(itemsStackFactory), 0, 0, BlockDirection.South);
            itemEntity = CollectBeltConveyorItems.CollectItem(_plusPlayerCoordinate, worldDataStore, blockConfig, entityFactory)[0];
            
            Assert.AreEqual(0.5, itemEntity.Position.X);
            Assert.AreEqual(0, itemEntity.Position.Y);
            Assert.AreEqual(0.25, itemEntity.Position.Z);


            
            worldDataStore.RemoveBlock(0, 0);
            worldDataStore.AddBlock(CreateOneItemInsertedItem(itemsStackFactory), 0, 0, BlockDirection.West);
            itemEntity = CollectBeltConveyorItems.CollectItem(_plusPlayerCoordinate, worldDataStore, blockConfig, entityFactory)[0];
            
            Assert.AreEqual(0.25, itemEntity.Position.X);
            Assert.AreEqual(0, itemEntity.Position.Y);
            Assert.AreEqual(0.5, itemEntity.Position.Z);


            //xy
            
            worldDataStore.RemoveBlock(0, 0);
            worldDataStore.AddBlock(CreateOneItemInsertedItem(itemsStackFactory), -1, -1, BlockDirection.North);

            
            itemEntity = CollectBeltConveyorItems.CollectItem(_minusPlayerCoordinate, worldDataStore, blockConfig, entityFactory)[0];
            
            Assert.AreEqual(-0.5, itemEntity.Position.X);
            Assert.AreEqual(0, itemEntity.Position.Y);
            Assert.AreEqual(-0.25, itemEntity.Position.Z); //-13/4-0.25


            
            worldDataStore.RemoveBlock(-1, -1);
            worldDataStore.AddBlock(CreateOneItemInsertedItem(itemsStackFactory), -1, -1, BlockDirection.East);
            itemEntity = CollectBeltConveyorItems.CollectItem(_minusPlayerCoordinate, worldDataStore, blockConfig, entityFactory)[0];
            
            Assert.AreEqual(-0.25, itemEntity.Position.X);
            Assert.AreEqual(0, itemEntity.Position.Y);
            Assert.AreEqual(-0.5, itemEntity.Position.Z);


            
            worldDataStore.RemoveBlock(-1, -1);
            worldDataStore.AddBlock(CreateOneItemInsertedItem(itemsStackFactory), -1, -1, BlockDirection.South);
            itemEntity = CollectBeltConveyorItems.CollectItem(_minusPlayerCoordinate, worldDataStore, blockConfig, entityFactory)[0];
            
            Assert.AreEqual(-0.5, itemEntity.Position.X);
            Assert.AreEqual(0, itemEntity.Position.Y);
            Assert.AreEqual(-0.75, itemEntity.Position.Z);


            
            worldDataStore.RemoveBlock(-1, -1);
            worldDataStore.AddBlock(CreateOneItemInsertedItem(itemsStackFactory), -1, -1, BlockDirection.West);
            itemEntity = CollectBeltConveyorItems.CollectItem(_minusPlayerCoordinate, worldDataStore, blockConfig, entityFactory)[0];
            
            Assert.AreEqual(-0.75, itemEntity.Position.X);
            Assert.AreEqual(0, itemEntity.Position.Y);
            Assert.AreEqual(-0.5, itemEntity.Position.Z);
        }



        ///     InstanceId

        [Test]
        public void ItemInstanceIdTest()
        {
            var (packet, serviceProvider) = new PacketResponseCreatorDiContainerGenerators().Create(TestModDirectory.ForUnitTestModDirectory);
            var itemsStackFactory = serviceProvider.GetService<ItemStackFactory>();
            var worldDataStore = serviceProvider.GetService<IWorldBlockDatastore>();

            var belt1 = CreateOneItemInsertedItem(itemsStackFactory);
            var belt2 = new VanillaBeltConveyor(1, 11, 1, itemsStackFactory, 4, TimeOfItemEnterToExit);
            
            worldDataStore.AddBlock(belt1, 0, 0, BlockDirection.North);
            worldDataStore.AddBlock(belt2, 0, 1, BlockDirection.North);

            //4
            var now = DateTime.Now;
            while (DateTime.Now - now < TimeSpan.FromMilliseconds(TimeOfItemEnterToExit)) GameUpdater.Update();

            
            var inventoryItemsField = typeof(VanillaBeltConveyor).GetField("_inventoryItems", BindingFlags.NonPublic | BindingFlags.Instance);
            var inventory2Items = (List<BeltConveyorInventoryItem>)inventoryItemsField.GetValue(belt2);

            
            Assert.AreEqual(1, inventory2Items.Count);
            //InstanceId
            Assert.AreEqual(ItemInstanceId, inventory2Items[0].ItemInstanceId);
        }


        private VanillaBeltConveyor CreateOneItemInsertedItem(ItemStackFactory itemsStackFactory)
        {
            var belt = new VanillaBeltConveyor(UnitTestModBlockId.BeltConveyorId, 10, 1, itemsStackFactory, 4, TimeOfItemEnterToExit);
            InsertItemToBeltConveyor(belt, new BeltConveyorInventoryItem(1, RemainingTime, 0, ItemInstanceId));
            return belt;
        }

        private void InsertItemToBeltConveyor(VanillaBeltConveyor beltConveyor, params BeltConveyorInventoryItem[] items)
        {
            //_inventoryItems
            var inventoryItemsField = typeof(VanillaBeltConveyor).GetField("_inventoryItems", BindingFlags.NonPublic | BindingFlags.Instance);
            var inventoryItems = (List<BeltConveyorInventoryItem>)inventoryItemsField.GetValue(beltConveyor);

            
            inventoryItems.AddRange(items);
        }
    }
}
#endif