using Game.Block.Interface;
using Game.Context;
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
                var entityId = BlockInstanceId.Create();
                
                var x = random.Next(-1000, 1000);
                var z = random.Next(-1000, 1000);
                var pos = new Vector3Int(x, 0, z);
                
                worldData.TryAddBlock(ForUnitTestModBlockId.MachineId, pos, BlockDirection.North, out var _);
                
                var output = worldData.GetBlock(pos);
                Assert.AreEqual(entityId, output.BlockInstanceId);
            }
        }
        
        
        [Test]
        public void AlreadyRegisteredEntityIdSecondTimeFailTest()
        {
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);
            var worldData = ServerContext.WorldBlockDatastore;
            
            var entityId = BlockInstanceId.Create();
            
            //TODO 同じIDになることない
            // worldData.TryAddBlock(ForUnitTestModBlockId.MachineId, new Vector3Int(1, 1), BlockDirection.North, out _);
            
            //座標だけ変えてintIDは同じ
            // var block2 = CreateMachine(entityId, new Vector3Int(10, 10), BlockDirection.North);
            // worldData.TryAddBlock(ForUnitTestModBlockId.MachineId, new Vector3Int(10, 10), BlockDirection.North, out _);
            // Assert.False(worldData.TryAddBlock(block2));
        }
        
        [Test]
        public void AlreadyCoordinateSecondTimeFailTest()
        {
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);
            
            var worldData = ServerContext.WorldBlockDatastore;
            
            //TODO 同じIDになることない
            // var block = CreateMachine(BlockInstanceId.Create(), new Vector3Int(1, 1), BlockDirection.North);
            // worldData.TryAddBlock(block);
            //
            // //座標だけ変えてintIDは同じ
            // var block2 = CreateMachine(BlockInstanceId.Create(), new Vector3Int(1, 1), BlockDirection.North);
            // Assert.False(worldData.TryAddBlock(block2));
        }
    }
}