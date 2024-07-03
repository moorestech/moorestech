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
                
                worldData.TryAddBlock(ForUnitTestModBlockId.MachineId, pos, BlockDirection.North, out _, entityId);
                
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
            worldData.TryAddBlock(ForUnitTestModBlockId.MachineId, new Vector3Int(1, 1), BlockDirection.North, out _, entityId);
            
            //座標だけ変えてintIDは同じ
            var result = worldData.TryAddBlock(ForUnitTestModBlockId.MachineId, new Vector3Int(10, 10), BlockDirection.North, out _, entityId);
            Assert.False(result);
        }
        
        [Test]
        public void AlreadyCoordinateSecondTimeFailTest()
        {
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);
            
            var worldData = ServerContext.WorldBlockDatastore;
            
            worldData.TryAddBlock(ForUnitTestModBlockId.MachineId, new Vector3Int(1, 1), BlockDirection.North, out _);
            
            //idだけ変えて座標は同じ
            var result = worldData.TryAddBlock(ForUnitTestModBlockId.MachineId, new Vector3Int(1, 1), BlockDirection.North, out _);
            Assert.False(result);
        }
    }
}