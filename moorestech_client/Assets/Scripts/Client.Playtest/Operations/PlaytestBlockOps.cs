using System;
using Client.Game.InGame.Block;
using Client.Game.InGame.Context;
using Core.Master;
using Cysharp.Threading.Tasks;
using Game.Block.Interface;
using Game.Context;
using Game.UnlockState;
using UnityEngine;

namespace Client.Playtest.Operations
{
    /// <summary>
    ///     ブロック設置・削除・出現待ちの操作群。Directはサーバー直叩き（インベントリ非消費・即時）
    ///     Block place/remove/spawn-wait helpers. Direct path hits the server datastore (no inventory cost, instant)
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

        public static void UnlockBlockServerSide(string blockName)
        {
            // サーバー側でアンロックし、UnlockedEventPacket経由でクライアントのビルドメニューへ同期させる
            // Unlock on the server; the UnlockedEventPacket syncs it to the client build menu
            var blockGuid = MasterHolder.BlockMaster.GetBlockMaster(ResolveBlockId(blockName)).BlockGuid;
            ServerContext.GetService<IGameUnlockStateDataController>().UnlockBlock(blockGuid);
        }

        public static IBlock PlaceBlockDirect(string blockName, Vector3Int position, BlockDirection direction)
        {
            // サーバーのデータストアへ直接設置する（本番プロトコル非経由）
            // Place directly into the server datastore (bypasses the production protocol)
            var blockId = ResolveBlockId(blockName);
            ServerContext.WorldBlockDatastore.TryAddBlock(blockId, position, direction, Array.Empty<BlockCreateParam>(), out var block);
            return block;
        }

        public static bool RemoveBlock(Vector3Int position)
        {
            return ServerContext.WorldBlockDatastore.RemoveBlock(position, BlockRemoveReason.ManualRemove);
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
