using System;
using Game.Block.Blocks.Machine;
using Game.Block.Event;
using Game.Block.RecipeConfig;
using Core.ConfigJson;
using Core.Item;
using Core.Item.Config;
using Game.Block.Interface;
using Game.World.Interface.DataStore;
using Game.World.Interface.Util;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server;
using Server.Boot;


using Test.Module.TestMod;

namespace Test.UnitTest.Game
{
    public class WorldBlockDatastoreTest
    {
        [Test]
        public void RegisteredDataCoordinateFromFetchTest()
        {
            var (packet, serviceProvider) = new PacketResponseCreatorDiContainerGenerators().Create(TestModDirectory.ForUnitTestModDirectory);
            var worldData = serviceProvider.GetService<IWorldBlockDatastore>();

            var random = new Random(131513);
            for (int i = 0; i < 10; i++)
            {
                var entityId = CreateBlockEntityId.Create();
                var ins = CreateMachine(1, entityId);

                int x = random.Next(-1000, 1000);
                int y = random.Next(-1000, 1000);

                worldData.AddBlock(ins, x, y, BlockDirection.North);
                var output = worldData.GetBlock(x, y);
                Assert.AreEqual(entityId, output.EntityId);
            }
        }


        [Test]
        public void AlreadyRegisteredEntityIdSecondTimeFailTest()
        {
            var (packet, serviceProvider) = new PacketResponseCreatorDiContainerGenerators().Create(TestModDirectory.ForUnitTestModDirectory);
            var worldData = serviceProvider.GetService<IWorldBlockDatastore>();

            var entityId = CreateBlockEntityId.Create();
            var i = CreateMachine(1, entityId);
            worldData.AddBlock(i, 1, 1, BlockDirection.North);

            //座標だけ変えてintIDは同じ
            var i2 = CreateMachine(1, entityId);
            Assert.False(worldData.AddBlock(i2, 10, 10, BlockDirection.North));
        }

        [Test]
        public void AlreadyCoordinateSecondTimeFailTest()
        {
            var (packet, serviceProvider) = new PacketResponseCreatorDiContainerGenerators().Create(TestModDirectory.ForUnitTestModDirectory);
            var worldData = serviceProvider.GetService<IWorldBlockDatastore>();

            var i = CreateMachine(1, CreateBlockEntityId.Create());
            worldData.AddBlock(i, 1, 1, BlockDirection.North);

            //座標だけ変えてintIDは同じ
            var i2 = CreateMachine(1, CreateBlockEntityId.Create());
            Assert.False(worldData.AddBlock(i2, 1, 1, BlockDirection.North));
        }

        private IBlockFactory _blockFactory;

        private VanillaMachineBase CreateMachine(int id, int entityId)
        {
            if (_blockFactory == null)
            {
                var (_, serviceProvider) = new PacketResponseCreatorDiContainerGenerators().Create(TestModDirectory.ForUnitTestModDirectory);
                _blockFactory = serviceProvider.GetService<IBlockFactory>();
            }

            var machine = _blockFactory.Create(id, entityId) as VanillaMachineBase;
            return machine;
        }
    }
}