using System;
using System.Threading;
using Client.Game.InGame.Block;
using Client.Game.InGame.Context;
using Client.Playtest.Core;
using Core.Master;
using Cysharp.Threading.Tasks;
using Game.Block.Interface;
using Game.Context;
using Game.SpaceElevator;
using Game.UnlockState;
using UnityEngine;
using VContainer;

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

        public static async UniTask UnlockBlockServerSide(CancellationToken cancellationToken, string blockName)
        {
            // サーバー側でアンロックし、UnlockedEventPacket経由でクライアントのビルドメニューへ同期させる
            // Unlock on the server; the UnlockedEventPacket syncs it to the client build menu
            var blockGuid = MasterHolder.BlockMaster.GetBlockMaster(ResolveBlockId(blockName)).BlockGuid;
            await PlaytestServerThreadAction.RunAsync(cancellationToken, $"unlock block {blockName}", () => ServerContext.GetService<IGameUnlockStateDataController>().UnlockBlock(blockGuid));
            // 解放通知がクライアントへ届くまで後続の割当を待たせる
            // Wait for the client unlock event before allowing the next assignment
            var clientState = ClientDIContext.DIContainer.DIContainerResolver.Resolve<IGameUnlockStateData>();
            var deadline = Time.realtimeSinceStartup + 15f;
            while (!clientState.BlockUnlockStateInfos.TryGetValue(blockGuid, out var info) || !info.IsUnlocked)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (deadline < Time.realtimeSinceStartup)
                    throw new TimeoutException($"Client unlock not reflected within 15s: {blockName}");
                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
            }
        }

        public static async UniTask<IBlock> PlaceBlockDirect(CancellationToken cancellationToken, string blockName, Vector3Int position, BlockDirection direction)
        {
            // サーバーのデータストアへtick末尾で直接設置する（本番プロトコル非経由）
            // Place directly into the server datastore at the tick end (bypasses the production protocol)
            var blockId = ResolveBlockId(blockName);
            IBlock block = null;
            var placed = false;
            await PlaytestServerThreadAction.RunAsync(cancellationToken, $"place {blockName} at {position}", () => placed = ServerContext.WorldBlockDatastore.TryAddBlock(blockId, position, direction, Array.Empty<BlockCreateParam>(), out block));
            if (!placed) throw new InvalidOperationException($"設置拒否 pos={position} block={blockName}");
            return block;
        }

        public static async UniTask<bool> RemoveBlock(CancellationToken cancellationToken, Vector3Int position)
        {
            var removed = false;
            await PlaytestServerThreadAction.RunAsync(cancellationToken, $"remove block at {position}", () =>
            {
                var block = ServerContext.WorldBlockDatastore.GetBlock(position);
                if (block != null && !ServerContext.GetService<SpaceElevatorConnectionService>().CanRemove(block)) return;
                removed = ServerContext.WorldBlockDatastore.RemoveBlock(CancellationToken cancellationToken, position, BlockRemoveReason.ManualRemove);
            });
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
