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

        [Test]
        public void CoordinatePlaceSubscriberReceivesMatchingOccupiedPositionTest()
        {
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var worldData = ServerContext.WorldBlockDatastore;
            var subscribedPos = new Vector3Int(0, 0, 3);
            var receivedCount = 0;
            var receivedPos = Vector3Int.zero;

            // 占有セルに対応する座標購読者だけに通知されることを確認する
            // Verify that only the subscriber for the occupied coordinate receives the event
            using var subscription = ServerContext.WorldBlockUpdateEvent.SubscribePlace(subscribedPos, properties =>
            {
                receivedCount++;
                receivedPos = properties.Pos;
            });
            var placed = worldData.TryAddBlock(ForUnitTestModBlockId.MultiBlock1, Vector3Int.zero, BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);

            Assert.IsTrue(placed);
            Assert.AreEqual(1, receivedCount);
            Assert.AreEqual(subscribedPos, receivedPos);
        }

        [Test]
        public void DisposedCoordinatePlaceSubscriberDoesNotReceiveEventTest()
        {
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var worldData = ServerContext.WorldBlockDatastore;
            var receivedCount = 0;

            // Dispose済み購読者を座標イベントから外す
            // Remove disposed subscribers from the coordinate event
            var subscription = ServerContext.WorldBlockUpdateEvent.SubscribePlace(Vector3Int.zero, _ => receivedCount++);
            subscription.Dispose();
            var placed = worldData.TryAddBlock(ForUnitTestModBlockId.MachineId, Vector3Int.zero, BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);

            Assert.IsTrue(placed);
            Assert.AreEqual(0, receivedCount);
        }
    }
}
