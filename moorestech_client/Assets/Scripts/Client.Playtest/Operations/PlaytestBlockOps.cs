using System;
using Client.Game.InGame.Block;
using Client.Game.InGame.Context;
using Client.Playtest.Core;
using Core.Master;
using Cysharp.Threading.Tasks;
using Game.Block.Interface;
using Game.Context;
using Game.UnlockState;
using UnityEngine;

namespace Client.Playtest.Operations
{
    /// <summary>
    ///     ブロック設置・削除・出現待ちの操作群。Directはサーバーtick末尾でデータストアを直接書く（インベントリ非消費）
    ///     Block place/remove/spawn-wait helpers. Direct path writes the server datastore at the tick end (no inventory cost)
    /// </summary>
    public static class PlaytestBlockOps
    {
        public static BlockId ResolveBlockId(string blockName)
        {
            foreach (var blockId in MasterHolder.BlockMaster.GetBlockAllIds())
            {
                if (MasterHolder.BlockMaster.GetBlockMaster(blockId).Name == blockName) return blockId;
            }
            throw new ArgumentException($"Block not found: {blockName}");
        }

        public static async UniTask UnlockBlockServerSide(string blockName)
        {
            // サーバー側でアンロックし、UnlockedEventPacket経由でクライアントのビルドメニューへ同期させる
            // Unlock on the server; the UnlockedEventPacket syncs it to the client build menu
            var blockGuid = MasterHolder.BlockMaster.GetBlockMaster(ResolveBlockId(blockName)).BlockGuid;
            await PlaytestServerThreadAction.RunAsync($"unlock block {blockName}", () => ServerContext.GetService<IGameUnlockStateDataController>().UnlockBlock(blockGuid));
        }

        public static async UniTask<IBlock> PlaceBlockDirect(string blockName, Vector3Int position, BlockDirection direction)
        {
            // サーバーのデータストアへtick末尾で直接設置する（本番プロトコル非経由）
            // Place directly into the server datastore at the tick end (bypasses the production protocol)
            var blockId = ResolveBlockId(blockName);
            IBlock block = null;
            await PlaytestServerThreadAction.RunAsync($"place {blockName} at {position}", () => ServerContext.WorldBlockDatastore.TryAddBlock(blockId, position, direction, Array.Empty<BlockCreateParam>(), out block));
            return block;
        }

        public static async UniTask<bool> RemoveBlock(Vector3Int position)
        {
            var removed = false;
            await PlaytestServerThreadAction.RunAsync($"remove block at {position}", () => removed = ServerContext.WorldBlockDatastore.RemoveBlock(position, BlockRemoveReason.ManualRemove));
            return removed;
        }

        public static IBlock GetBlock(Vector3Int position)
        {
            return ServerContext.WorldBlockDatastore.GetBlock(position);
        }

        public static async UniTask<BlockGameObject> WaitBlockGameObjectSpawn(Vector3Int position, float timeoutSeconds)
        {
            // クライアント側のBlockGameObject出現をフレームポーリングで待つ
            // Poll per frame until the client-side BlockGameObject spawns
            var startTime = Time.realtimeSinceStartup;
            while (true)
            {
                if (ClientDIContext.BlockGameObjectDataStore.TryGetBlockGameObject(position, out var blockGameObject))
                {
                    return blockGameObject;
                }
                if (timeoutSeconds < Time.realtimeSinceStartup - startTime)
                {
                    throw new TimeoutException($"client BlockGameObject not spawned at {position} within {timeoutSeconds}s");
                }
                await UniTask.Yield();
            }
        }
    }
}
