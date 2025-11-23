using System;
using Game.Block;
using Game.Block.Interface;
using Game.World.Interface.DataStore;
using NUnit.Framework;
using UnityEngine;
using System.Collections.Generic;
using Core.Master;
using Game.Block.Interface.Component;
using Game.Block.Interface.State;
using Mooresmaster.Model.BlocksModule;

namespace Tests.UnitTest.Game
{
    public class BlockRemoverTest
    {
        [Test]
        public void RemoveBlock_CallsWorldBlockDatastoreRemove()
        {
            // Arrange
            var mockWorldBlockDatastore = new MockWorldBlockDatastore();
            var mockServiceProvider = new MockServiceProvider(mockWorldBlockDatastore);

            var blockRemover = new BlockRemover(mockServiceProvider);

            var positionInfo = new BlockPositionInfo(new Vector3Int(1, 2, 3), BlockDirection.North, new Vector3Int(1, 1, 1));
            var removalType = BlockRemovalType.Broken;

            // Act
            blockRemover.RemoveBlock(positionInfo, removalType);

            // Assert
            Assert.AreEqual(1, mockWorldBlockDatastore.RemoveBlockCallCount);
            Assert.AreEqual(positionInfo.OriginalPos, mockWorldBlockDatastore.LastRemovePos);
        }

        class MockServiceProvider : IServiceProvider
        {
            private readonly object _service;
            public MockServiceProvider(object service) => _service = service;
            public object GetService(Type serviceType) => _service;
        }

        class MockWorldBlockDatastore : IWorldBlockDatastore
        {
            public int RemoveBlockCallCount = 0;
            public Vector3Int LastRemovePos;

            public bool RemoveBlock(Vector3Int pos)
            {
                RemoveBlockCallCount++;
                LastRemovePos = pos;
                return true;
            }

            // Implement other members as throwing or dummy
            public IReadOnlyDictionary<BlockInstanceId, WorldBlockData> BlockMasterDictionary => throw new NotImplementedException();
            public IObservable<(BlockState state, WorldBlockData blockData)> OnBlockStateChange => throw new NotImplementedException();
            public bool TryAddBlock(BlockId blockId, Vector3Int position, BlockDirection direction, BlockCreateParam[] createParams, out IBlock block) => throw new NotImplementedException();
            public bool TryAddLoadedBlock(Guid blockGuid, BlockInstanceId blockInstanceId, Dictionary<string, string> componentStates, Vector3Int position, BlockDirection direction, out IBlock block) => throw new NotImplementedException();
            public IBlock GetBlock(Vector3Int pos) => throw new NotImplementedException();
            public IBlock GetBlock(BlockInstanceId blockInstanceId) => throw new NotImplementedException();
            public IBlock GetBlock(IBlockComponent component) => throw new NotImplementedException();
            public WorldBlockData GetOriginPosBlock(Vector3Int pos) => throw new NotImplementedException();
            public Vector3Int GetBlockPosition(BlockInstanceId blockInstanceId) => throw new NotImplementedException();
            public BlockDirection GetBlockDirection(Vector3Int pos) => throw new NotImplementedException();
            public List<BlockJsonObject> GetSaveJsonObject() => throw new NotImplementedException();
            public void LoadBlockDataList(List<BlockJsonObject> saveBlockDataList) => throw new NotImplementedException();
        }
    }
}
