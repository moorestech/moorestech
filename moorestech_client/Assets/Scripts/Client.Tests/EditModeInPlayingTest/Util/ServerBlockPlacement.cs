using System;
using System.Threading;
using Core.Master;
using Cysharp.Threading.Tasks;
using Game.Block.Interface;
using Game.Context;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.EditModeInPlayingTest.Util
{
    internal sealed class ServerBlockPlacement : ServerTestAction
    {
        private readonly BlockId _blockId;
        private readonly Vector3Int _position;
        private readonly BlockDirection _direction;
        private IBlock _block;
        private bool _placed;

        private ServerBlockPlacement(BlockId blockId, Vector3Int position, BlockDirection direction)
        {
            _blockId = blockId;
            _position = position;
            _direction = direction;
        }

        internal static async UniTask<IBlock> PlaceAsync(string blockName, Vector3Int position, BlockDirection direction)
        {
            var callerThread = Thread.CurrentThread.ManagedThreadId;
            var action = new ServerBlockPlacement(EditModeInPlayingTestUtil.FindBlockIdByName(blockName), position, direction);
            await action.ExecuteAsync();
            // 設置成功とスレッド所有を検証する
            // Verify placement success and thread ownership
            Assert.AreNotEqual(callerThread, action.ServerThreadId, "placement must run on the server thread");
            Assert.IsTrue(action._placed, $"could not place {blockName} at {position}");
            return action._block;
        }

        protected override void ExecuteOnServer()
        {
            _placed = ServerContext.WorldBlockDatastore.TryAddBlock(
                _blockId, _position, _direction, Array.Empty<BlockCreateParam>(), out _block);
        }
    }
}
