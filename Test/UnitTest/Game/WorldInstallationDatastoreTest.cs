using System;
using Core.Block.BlockFactory;
using Core.Block.Blocks.Machine;
using Core.Block.RecipeConfig;
using Core.Item;
using Core.Item.Config;
using Game.World.Interface.DataStore;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server;
using Test.Module.TestConfig;
using IntId = World.Util.IntId;

namespace Test.UnitTest.Game
{
    public class WorldBlockDatastoreTest
    {

        [Test]
        public void RegisteredDataCoordinateFromFetchTest()
        {
            var (packet, serviceProvider) = new PacketResponseCreatorDiContainerGenerators().Create();
            var worldData = serviceProvider.GetService<IWorldBlockDatastore>();
            
            var random = new Random(131513);
            for (int i = 0; i < 10; i++)
            {
                var intId = IntId.NewIntId();
                var ins =  CreateMachine(1, intId);

                int x = random.Next(-1000, 1000);
                int y = random.Next(-1000, 1000);
                
                worldData.AddBlock(ins,x,y,BlockDirection.North);
                var output = worldData.GetBlock(x,y);
                Assert.AreEqual(intId, output.GetIntId());
            }
        }


        [Test]
        public void AlreadyRegisteredIntIdSecondTimeFailTest()
        {
            var (packet, serviceProvider) = new PacketResponseCreatorDiContainerGenerators().Create();
            var worldData = serviceProvider.GetService<IWorldBlockDatastore>();
            
            var intId = IntId.NewIntId();
            var i =  CreateMachine(1, intId);
            worldData.AddBlock(i,1,1,BlockDirection.North);
            
            //座標だけ変えてintIDは同じ
            var i2 =  CreateMachine(1, intId);
            Assert.False(worldData.AddBlock(i2,10,10,BlockDirection.North));
        }

        [Test]
        public void AlreadyCoordinateSecondTimeFailTest()
        {
            var (packet, serviceProvider) = new PacketResponseCreatorDiContainerGenerators().Create();
            var worldData = serviceProvider.GetService<IWorldBlockDatastore>();

            var i =  CreateMachine(1, IntId.NewIntId());
            worldData.AddBlock(i,1,1,BlockDirection.North);
            
            //座標だけ変えてintIDは同じ
            var i2 =  CreateMachine(1, IntId.NewIntId());
            Assert.False(worldData.AddBlock(i2,1,1,BlockDirection.North));
        }
        
        private BlockFactory _blockFactory;
        private VanillaMachine CreateMachine(int id,int indId)
        {
            if (_blockFactory == null)
            {
                var itemStackFactory = new ItemStackFactory(new TestItemConfig());
                _blockFactory = new BlockFactory(new AllMachineBlockConfig(),new VanillaIBlockTemplates(new TestMachineRecipeConfig(itemStackFactory),itemStackFactory));
            }
            var machine = _blockFactory.Create(id, indId) as VanillaMachine;
            return machine;
        }
    }
}