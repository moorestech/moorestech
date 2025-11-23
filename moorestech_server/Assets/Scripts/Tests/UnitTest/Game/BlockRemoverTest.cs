using System;
using System.Collections.Generic;
using Core.Master;
using Game.Block;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.State;
using Game.World.Interface.DataStore;
using NUnit.Framework;
using UniRx;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.UnitTest.Game
{
    public class BlockRemoverTest
    {
        [Test]
        public void RemoveBlock_DelegatesToWorldDatastoreAndLogsReason()
        {
            // ブロック除去の呼び出しとログを検証する
            // Verify world removal delegation and removal reason logging
            var datastore = new RecordingWorldBlockDatastore();
            var remover = new BlockRemover(datastore);
            var position = new BlockPositionInfo(new Vector3Int(2, 0, -1), BlockDirection.North, Vector3Int.one);

            LogAssert.Expect(LogType.Log, "Block removal: Position=(2, 0, -1), Type=Broken");

            remover.RemoveBlock(position, BlockRemovalType.Broken);

            Assert.IsTrue(datastore.RemoveCalled);
            Assert.AreEqual(position.OriginalPos, datastore.LastRemovedPosition);
        }

        private class RecordingWorldBlockDatastore : IWorldBlockDatastore
        {
            public IReadOnlyDictionary<BlockInstanceId, WorldBlockData> BlockMasterDictionary { get; } = new Dictionary<BlockInstanceId, WorldBlockData>();
            public IObservable<(BlockState state, WorldBlockData blockData)> OnBlockStateChange { get; } = Observable.Empty<(BlockState, WorldBlockData)>();

            public bool RemoveCalled { get; private set; }
            public Vector3Int LastRemovedPosition { get; private set; }

            public bool TryAddBlock(BlockId blockId, Vector3Int position, BlockDirection direction, BlockCreateParam[] createParams, out IBlock block)
            {
                block = null;
                return false;
            }
            public bool TryAddLoadedBlock(Guid blockGuid, BlockInstanceId blockInstanceId, Dictionary<string, string> componentStates, Vector3Int position, BlockDirection direction, out IBlock block) => throw new NotImplementedException();
            public bool RemoveBlock(Vector3Int pos)
            {
                RemoveCalled = true;
                LastRemovedPosition = pos;
                return true;
            }

            public IBlock GetBlock(Vector3Int pos) => null;
            public IBlock GetBlock(BlockInstanceId blockInstanceId) => null;
            public IBlock GetBlock(IBlockComponent component) => null;
            public WorldBlockData GetOriginPosBlock(Vector3Int pos) => null;
            public Vector3Int GetBlockPosition(BlockInstanceId blockInstanceId) => Vector3Int.zero;
            public BlockDirection GetBlockDirection(Vector3Int pos) => BlockDirection.North;
            public List<BlockJsonObject> GetSaveJsonObject() => new();
            public void LoadBlockDataList(List<BlockJsonObject> saveBlockDataList)
            {
            }
        }
    }
}
