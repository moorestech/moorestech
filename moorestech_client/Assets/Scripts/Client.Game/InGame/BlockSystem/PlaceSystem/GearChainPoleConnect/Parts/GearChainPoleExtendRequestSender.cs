using System;
using System.Threading;
using Client.Game.InGame.Block;
using Client.Game.InGame.Context;
using Core.Master;
using Cysharp.Threading.Tasks;
using Server.Protocol.PacketResponse;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.GearChainPoleConnect.Parts
{
    /// <summary>
    /// 歯車チェーンポール延長プロトコルの送信と応答後の起点引き継ぎを管理する。
    /// 世代カウンタにより、無効化や再選択後に古い応答が起点を上書きするのを防ぐ。
    /// Manages sending the gear chain pole extend protocol and source hand-off after response.
    /// A generation counter prevents stale responses from overwriting the source after disable or re-selection.
    /// </summary>
    public class GearChainPoleExtendRequestSender
    {
        private readonly BlockGameObjectDataStore _blockGameObjectDataStore;

        private int _generation;

        public bool IsAwaitingResponse { get; private set; }

        public GearChainPoleExtendRequestSender(BlockGameObjectDataStore blockGameObjectDataStore)
        {
            _blockGameObjectDataStore = blockGameObjectDataStore;
        }

        /// <summary>
        /// 進行中の応答を無効化する（無効化・起点再選択時に呼ぶ）
        /// Invalidate pending responses (call on disable or source re-selection)
        /// </summary>
        public void Invalidate()
        {
            _generation++;
            IsAwaitingResponse = false;
        }

        /// <summary>
        /// 延長または孤立設置のリクエストを送信し、成功時に新規ポールのコライダーを通知する
        /// Send an extend or isolated place request and notify the new pole collider on success
        /// </summary>
        public void Send(Vector3Int? fromPos, int poleSlot, PlaceInfo placeInfo, ItemId chainItemId, Action<GearChainPoleConnectAreaCollider> onPlacedPoleResolved)
        {
            var generation = ++_generation;
            IsAwaitingResponse = true;

            UniTask.Create(async () =>
            {
                var response = fromPos.HasValue
                    ? await ClientContext.VanillaApi.Response.ExtendGearChainPole(fromPos.Value, poleSlot, placeInfo, chainItemId, CancellationToken.None)
                    : await ClientContext.VanillaApi.Response.PlaceIsolatedGearChainPole(poleSlot, placeInfo, CancellationToken.None);

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
                if (!_blockGameObjectDataStore.TryGetBlockGameObject(placedPos, out var placedBlock)) return;

                var collider = placedBlock.GetComponentInChildren<GearChainPoleConnectAreaCollider>();
                if (collider != null) onPlacedPoleResolved(collider);
            });
        }
    }
}
