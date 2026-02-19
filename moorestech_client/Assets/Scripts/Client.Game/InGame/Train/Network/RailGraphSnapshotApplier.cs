using Client.Game.InGame.Train.RailGraph;
using Client.Game.InGame.Train.Unit;
using Client.Network.API;
using Game.Train.RailGraph;
using UnityEngine;
using VContainer.Unity;

namespace Client.Game.InGame.Train.Network
{
    /// <summary>
    ///     RailGraph差分の初期適用から再同期までを担うキャッシュ反映サービス
    ///     Service that applies the initial RailGraph snapshot and future resync payloads
    /// </summary>
    public sealed class RailGraphSnapshotApplier : IInitializable
    {
        private readonly RailGraphClientCache _cache;
        private readonly InitialHandshakeResponse _initialHandshakeResponse;
        private readonly ClientStationReferenceRegistry _stationReferenceRegistry;
        private readonly TrainUnitTickState _tickState;

        public RailGraphSnapshotApplier(
            RailGraphClientCache cache,
            InitialHandshakeResponse initialHandshakeResponse,
            ClientStationReferenceRegistry stationReferenceRegistry,
            TrainUnitTickState tickState)
        {
            _cache = cache;
            _initialHandshakeResponse = initialHandshakeResponse;
            _stationReferenceRegistry = stationReferenceRegistry;
            _tickState = tickState;
        }

        public void Initialize()
        {
            // 初回ハンドシェイクのスナップショットを即座に適用
            // Apply the handshake snapshot immediately after construction
            ApplySnapshot(_initialHandshakeResponse?.RailGraphSnapshot);
        }

        public void ApplySnapshot((RailGraphSnapshot snapshot, uint tickSequenceId)? snapshotResponse)
        {
            if (!snapshotResponse.HasValue)
            {
                return;
            }

            var (snapshot, tickSequenceId) = snapshotResponse.Value;
            var unifiedId = TrainTickUnifiedIdUtility.CreateTickUnifiedId(snapshot.GraphTick, tickSequenceId);
            if (unifiedId < _tickState.GetAppliedTickUnifiedId())
            {
                // 遅延rail snapshotが既に適用済み範囲より古い場合は破棄する。
                // Ignore delayed rail snapshots older than the applied sequence baseline.
                Debug.LogWarning(
                    "[RailGraphSnapshotApplier] Ignored stale rail snapshot. " +
                    $"graphTick={snapshot.GraphTick}, graphTickSequenceId={tickSequenceId}, " +
                    $"appliedTickUnifiedId={_tickState.GetAppliedTickUnifiedId()}");
                return;
            }

            // スナップショットが空のときは何もしない
            // Skip when snapshot payload is missing or empty
            if (snapshot.Nodes == null || snapshot.Nodes.Count == 0)
            {
                return;
            }

            // ノード最大ID算出と配列サイズ確定はcache側に移譲
            // Max node id resolution and buffer sizing are delegated to cache.
            // ノード情報を事前確保コンテナへ流し込む処理もcache側で実施
            // Node/container population is also executed inside cache.
            // まとめてキャッシュへ反映
            // Commit prepared data to cache
            _cache.ApplySnapshot(snapshot);
            // 駅参照をキャッシュへ反映する
            // Apply station references to cache.
            _stationReferenceRegistry.ApplyStationReferences();
            _tickState.RecordAppliedTickUnifiedId(unifiedId);
        }
    }
}
