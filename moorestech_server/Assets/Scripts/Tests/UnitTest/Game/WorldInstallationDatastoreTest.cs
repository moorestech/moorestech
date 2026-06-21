using System.Collections.Generic;
using Game.Block.Interface;
using Game.Context;
using Game.World.Interface.DataStore;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;
using Random = System.Random;
using System;

namespace Tests.UnitTest.Game
{
    public class WorldBlockDatastoreTest
    {
        [Test]
        public void RegisteredDataCoordinateFromFetchTest()
        {
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var worldData = ServerContext.WorldBlockDatastore;
            
            var random = new Random(131513);
            for (var i = 0; i < 10; i++)
            {
                var x = random.Next(-1000, 1000);
                var z = random.Next(-1000, 1000);
                var pos = new Vector3Int(x, 0, z);
                
                worldData.TryAddBlock(ForUnitTestModBlockId.MachineId, pos, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var block);
                
                var output = worldData.GetBlock(pos);
                Assert.AreEqual(block.BlockInstanceId, output.BlockInstanceId);
            }
        }
        
        
        [Test]
        public void AlreadyRegisteredEntityIdSecondTimeFailTest()
        {
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var worldData = ServerContext.WorldBlockDatastore;
            
            var entityId = BlockInstanceId.Create();
            
            //TODO 同じIDになることない
            worldData.TryAddBlock(ForUnitTestModBlockId.MachineId, new Vector3Int(1, 1), BlockDirection.North, Array.Empty<BlockCreateParam>(), out var originalBlock);
            var blockGuid = originalBlock.BlockGuid;
            var state = originalBlock.GetSaveState();
            
            // 同じインスタンスIDでロードを試行する
            // Try loading with the same instance ID
            var loadList = new List<BlockJsonObject> { new(new Vector3Int(10, 10), blockGuid.ToString(), originalBlock.BlockInstanceId.AsPrimitive(), state, (int)BlockDirection.North) };
            worldData.LoadBlockDataList(loadList);
            
            Assert.AreEqual(1, worldData.BlockMasterDictionary.Count);
            Assert.AreEqual(new Vector3Int(1, 1), worldData.GetBlockPosition(originalBlock.BlockInstanceId));
        }
        
        [Test]
        public void AlreadyCoordinateSecondTimeFailTest()
        {
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            
            var worldData = ServerContext.WorldBlockDatastore;
            
            worldData.TryAddBlock(ForUnitTestModBlockId.MachineId, new Vector3Int(1, 1), BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
            
            //idだけ変えて座標は同じ
            var result = worldData.TryAddBlock(ForUnitTestModBlockId.MachineId, new Vector3Int(1, 1), BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
            Assert.False(result);
        }
    }
}
