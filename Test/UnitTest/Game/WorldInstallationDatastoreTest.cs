using System;
using Core.Block;
using Core.Block.Config;
using Core.Block.Machine;
using Core.Block.Machine.util;
using Core.Block.RecipeConfig;
using Core.Item;
using Core.Item.Config;
using Core.Util;
using NUnit.Framework;
using World;
using World.Event;
using IntId = World.IntId;

namespace Test.UnitTest.Game
{
    public class WorldBlockDatastoreTest
    {
        [Test]
        public void DataInsertAndOutputTest()
        {
            var worldData = new WorldBlockDatastore(new BlockPlaceEvent());
            
            var intId = IntId.NewIntId();
            var i =  CreateMachine(1,intId);
            worldData.AddBlock(i,1,1,i);
            var output = worldData.GetBlock(intId);
            Assert.AreEqual(intId, output.GetIntId());

        }

        [Test]
        public void RegisteredDataCoordinateFromFetchTest()
        {
            var worldData = new WorldBlockDatastore(new BlockPlaceEvent());
            
            var random = new Random(131513);
            for (int i = 0; i < 10; i++)
            {
                var intId = IntId.NewIntId();
                var ins =  CreateMachine(1, intId);

                int x = random.Next(-1000, 1000);
                int y = random.Next(-1000, 1000);
                
                worldData.AddBlock(ins,x,y,ins);
                var output = worldData.GetBlock(x,y);
                Assert.AreEqual(intId, output.GetIntId());
            }
        }


        [Test]
        public void AlreadyRegisteredIntIdSecondTimeFailTest()
        {
            var worldData = new WorldBlockDatastore(new BlockPlaceEvent());
            
            var intId = IntId.NewIntId();
            var i =  CreateMachine(1, intId);
            worldData.AddBlock(i,1,1,i);
            
            //座標だけ変えてintIDは同じ
            var i2 =  CreateMachine(1, intId);
            Assert.False(worldData.AddBlock(i2,10,10,i2));
        }

        [Test]
        public void AlreadyCoordinateSecondTimeFailTest()
        {
            var worldData = new WorldBlockDatastore(new BlockPlaceEvent());

            var i =  CreateMachine(1, IntId.NewIntId());
            worldData.AddBlock(i,1,1,i);
            
            //座標だけ変えてintIDは同じ
            var i2 =  CreateMachine(1, IntId.NewIntId());
            Assert.False(worldData.AddBlock(i2,1,1,i2));
        }
        

        private IBlockInventory nullInventory = new NullIBlockInventory();
        private IBlockConfig blockConfig = new TestBlockConfig();
        private IItemConfig _itemConfig = new TestItemConfig();
        private ItemStackFactory _itemStackFactory;
        private IMachineRecipeConfig machineRecipeConfig;
        bool init = false;

        private NormalMachine CreateMachine(int id,int intId)
        {
            if (!init)
            {
                init = true;
                nullInventory = new NullIBlockInventory();
                blockConfig = new TestBlockConfig();
                _itemConfig = new TestItemConfig();
                _itemStackFactory = new ItemStackFactory(_itemConfig);
                machineRecipeConfig = new TestMachineRecipeConfig(_itemStackFactory);
            }

            return NormalMachineFactory.Create(5, intId,nullInventory, blockConfig, machineRecipeConfig,_itemStackFactory);
        }
    }
}