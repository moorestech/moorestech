using System.Threading;
using Client.Game.InGame.Block;
using Client.Game.InGame.Context;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.GearChainPoleConnect.Parts
{
    /// <summary>
    /// 歯車チェーンポール延長プロトコルの送信と、応答で確定した引き継ぎ先ポールの保持。
    /// コールバックは持たず、結果は上位がTryConsumePlacedPoleでループ先頭から取り込む一方向構造。
    /// 世代カウンタにより、無効化や再選択後に古い応答が結果を上書きするのを防ぐ。
    /// Sends the gear chain pole extend protocol and holds the resolved next-source pole from the response.
    /// No callbacks: the upper layer consumes the result via TryConsumePlacedPole at the top of its loop, keeping the flow one-way.
    /// A generation counter prevents stale responses from overwriting the result after invalidation or re-selection.
    /// </summary>
    public class GearChainPoleExtendRequestSender
    {
        private readonly BlockGameObjectDataStore _blockGameObjectDataStore;

        private int _generation;
        private GearChainPoleConnectAreaCollider _resolvedPole;

        public bool IsAwaitingResponse { get; private set; }

        public GearChainPoleExtendRequestSender(BlockGameObjectDataStore blockGameObjectDataStore)
        {
            _blockGameObjectDataStore = blockGameObjectDataStore;
        }

        /// <summary>
        /// 進行中の応答と未取り込みの結果を無効化する（無効化・起点再選択時に呼ぶ）
        /// Invalidate pending responses and any unconsumed result (call on disable or source re-selection)
        /// </summary>
        public void Invalidate()
        {
            _generation++;
            IsAwaitingResponse = false;
            _resolvedPole = null;
        }

        /// <summary>
        /// 応答で確定した引き継ぎ先ポールを一度だけ取り出す
        /// Consume the resolved next-source pole from the response exactly once
        /// </summary>
        public bool TryConsumePlacedPole(out GearChainPoleConnectAreaCollider placedPole)
        {
            placedPole = _resolvedPole;
            _resolvedPole = null;
            return placedPole != null;
        }

        /// <summary>
        /// 延長または孤立設置のリクエストを送信する。成功時の引き継ぎ先はTryConsumePlacedPoleで取り込む
        /// Send an extend or isolated place request. Consume the resulting next source via TryConsumePlacedPole
        /// </summary>
        public void Send(GearChainPoleExtendSendCommand command)
        {
            var generation = ++_generation;
            IsAwaitingResponse = true;
            _resolvedPole = null;

            UniTask.Create(async () =>
            {
                var response = command.FromPos.HasValue
                    ? await ClientContext.VanillaApi.Response.ExtendGearChainPole(command.FromPos.Value, command.PoleSlot, command.PlaceInfo, command.ChainItemId, CancellationToken.None)
                    : await ClientContext.VanillaApi.Response.PlaceIsolatedGearChainPole(command.PoleSlot, command.PlaceInfo, CancellationToken.None);

                // 世代が進んでいたら結果を破棄する（フラグは進めた側が管理済み）
                // Discard the result when the generation has advanced (the advancer manages the flag)
                if (generation != _generation) return;

                // タイムアウト等の応答なし・失敗時は待ち状態を解除する
                // Release the awaiting state on no-response (timeout) or failure
                if (response == null || !response.IsSuccess)
                {
                    IsAwaitingResponse = false;
                    return;
                }

                // 新規ポールの生成を待って起点引き継ぎ先を解決する（引き継ぎ確定まで待ち状態を維持し孤立設置化を防ぐ）
                // Wait for the new pole to spawn and resolve the next source (stay awaiting until hand-off to prevent isolated placement)
                var placedPos = (Vector3Int)response.PlacedPolePos;
                using var spawnWaitCancellation = new CancellationTokenSource();
                await UniTask.WhenAny(
                    UniTask.WaitForSeconds(1f),
                    UniTask.WaitUntil(() => _blockGameObjectDataStore.TryGetBlockGameObject(placedPos, out _), cancellationToken: spawnWaitCancellation.Token).SuppressCancellationThrow());

                // 敗者側のポーリングを止める
                // Stop the losing poll task
                spawnWaitCancellation.Cancel();

                if (generation != _generation) return;
                IsAwaitingResponse = false;
                if (!command.CanContinueExtension) return;
                if (!_blockGameObjectDataStore.TryGetBlockGameObject(placedPos, out var placedBlock)) return;

                _resolvedPole = placedBlock.GetComponentInChildren<GearChainPoleConnectAreaCollider>();
            });
        }
    }
}
