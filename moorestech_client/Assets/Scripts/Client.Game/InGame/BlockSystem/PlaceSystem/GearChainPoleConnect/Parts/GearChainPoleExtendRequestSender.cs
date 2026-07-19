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
    /// Sends the gear chain pole extend protocol and holds the resolved next-source pole from the response.
    /// No callbacks: the upper layer consumes the result via TryConsumePlacedPole at the top of its loop, keeping the flow one-way.
    /// </summary>
    public class GearChainPoleExtendRequestSender
    {
        // 新規ポールのエンティティ生成を待つ上限秒数
        // Timeout seconds for waiting the new pole entity to spawn
        private const float PoleSpawnWaitSeconds = 1f;

        private readonly BlockGameObjectDataStore _blockGameObjectDataStore;

        // 応答待ち中に無効化や再送信が起きても、最後の1件以外の古い応答を確実に捨てるための世代トークン。
        // Sendは自分の世代番号をキャプチャし、応答到着時に現在値と一致した場合のみ結果を反映する。
        // これがないと「送信→起点選び直し→古い応答が遅れて到着」の順序で、破棄済みの結果が起点を上書きしてしまう。
        // Generation token that guarantees only the latest response survives invalidation or re-sending while awaiting.
        // Each Send captures its own generation and applies the result only when it still matches on arrival.
        // Without this, the order send → re-select source → stale response arrives would overwrite the source with a discarded result.
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
                // 応答を待ち、成功時のみ新規ポールの生成を待って引き継ぎ先を解決する
                // Await the response, then resolve the next source only on success by waiting for the new pole to spawn
                var response = command.FromPos.HasValue
                    ? await ClientContext.VanillaApi.Response.ExtendGearChainPole(command.FromPos.Value, command.PoleBlockId, command.PlaceInfo, command.ConnectToolGuid, CancellationToken.None)
                    : await ClientContext.VanillaApi.Response.PlaceIsolatedGearChainPole(command.PoleBlockId, command.PlaceInfo, CancellationToken.None);
                var placedPole = response is { IsSuccess: true } ? await WaitForPlacedPole((Vector3Int)response.PlacedPolePos) : null;

                // 世代が進んでいたら応答ごと破棄する（フラグは進めた側が管理済み）
                // Discard everything when the generation has advanced (the advancer manages the flag)
                if (generation != _generation) return;
                IsAwaitingResponse = false;
                if (command.CanContinueExtension) _resolvedPole = placedPole;
            });
        }

        private async UniTask<GearChainPoleConnectAreaCollider> WaitForPlacedPole(Vector3Int placedPos)
        {
            // エンティティ生成をタイムアウト付きで毎フレーム確認する（引き継ぎ確定まで待ち状態を維持し孤立設置化を防ぐ）
            // Poll the entity spawn every frame with a timeout (stay awaiting until hand-off to prevent isolated placement)
            var startTime = Time.time;
            while (Time.time - startTime < PoleSpawnWaitSeconds)
            {
                if (_blockGameObjectDataStore.TryGetBlockGameObject(placedPos, out var placedBlock)) return placedBlock.GetComponentInChildren<GearChainPoleConnectAreaCollider>();
                await UniTask.NextFrame();
            }

            return null;
        }
    }
}
