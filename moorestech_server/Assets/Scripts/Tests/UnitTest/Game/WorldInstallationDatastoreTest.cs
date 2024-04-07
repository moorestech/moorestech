using Game.Block.Blocks.Machine;
using Game.Block.Interface;
using Game.Context;
using Game.World.Interface.DataStore;
using Game.World.Interface.Util;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;
using Random = System.Random;

namespace Tests.UnitTest.Game
{
    public class WorldBlockDatastoreTest
    {
        [Test]
        public void RegisteredDataCoordinateFromFetchTest()
        {
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);
            var worldData = ServerContext.WorldBlockDatastore;

            var random = new Random(131513);
            for (var i = 0; i < 10; i++)
            {
                var entityId = CreateBlockEntityId.Create();

                var x = random.Next(-1000, 1000);
                var z = random.Next(-1000, 1000);
                var pos = new Vector3Int(x, 0, z);

                var ins = CreateMachine(1, entityId, pos, BlockDirection.North);
                worldData.AddBlock(ins);

                var output = worldData.GetBlock(pos);
                Assert.AreEqual(entityId, output.EntityId);
            }
        }


        [Test]
        public void AlreadyRegisteredEntityIdSecondTimeFailTest()
        {
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);
            var worldData = ServerContext.WorldBlockDatastore;

            var entityId = CreateBlockEntityId.Create();

            var block = CreateMachine(1, entityId, new Vector3Int(1, 1), BlockDirection.North);
            worldData.AddBlock(block);

            //座標だけ変えてintIDは同じ
            var block2 = CreateMachine(1, entityId, new Vector3Int(10, 10), BlockDirection.North);
            Assert.False(worldData.AddBlock(block2));
        }

        [Test]
        public void AlreadyCoordinateSecondTimeFailTest()
        {
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);

            var worldData = ServerContext.WorldBlockDatastore;

            var block = CreateMachine(1, CreateBlockEntityId.Create(), new Vector3Int(1, 1), BlockDirection.North);
            worldData.AddBlock(block);

            //座標だけ変えてintIDは同じ
            var block2 = CreateMachine(1, CreateBlockEntityId.Create(), new Vector3Int(1, 1), BlockDirection.North);
            Assert.False(worldData.AddBlock(block2));
        }

        private VanillaMachineBase CreateMachine(int id, int entityId, Vector3Int pos, BlockDirection direction)
        {
            var posInfo = new BlockPositionInfo(pos, direction, Vector3Int.one);
            var machine = ServerContext.BlockFactory.Create(id, entityId, posInfo) as VanillaMachineBase;
            return machine;
        }
    }
}