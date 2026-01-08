using Game.Train.Train;
using Server.Util.MessagePack;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Client.Game.InGame.Train
{
    // RailGraphのキャッシュと同じように列車の状態を保持する
    // Cache that mirrors every train unit similar to the RailGraph cache
    public sealed class TrainUnitClientCache
    {
        // ローカルで追跡する列車一覧
        // Internal dictionary holding every tracked train
        private readonly RailGraphClientCache _railGraphProvider;
        private readonly Dictionary<Guid, ClientTrainUnit> _units = new();

        // 最新の適用済みtick
        // Latest tick that has been fully applied
        public long LastServerTick { get; private set; }

        // 列車一覧の読み取り専用ビュー
        // Read-only view for external systems
        public IReadOnlyDictionary<Guid, ClientTrainUnit> Units => _units;

        public TrainUnitClientCache(RailGraphClientCache railGraphProvider)
        {
            // レールグラフプロバイダを保持する
            // Keep the rail graph provider reference
            _railGraphProvider = railGraphProvider;
        }

        // 初期スナップショットでキャッシュ全体を入れ替える
        // Replace the entire cache when a full snapshot arrives
        public void OverrideAll(IReadOnlyList<TrainUnitSnapshotBundle> snapshots, long serverTick)
        {
            _units.Clear();
            if (snapshots == null)
            {
                LastServerTick = serverTick;
                return;
            }

            for (var i = 0; i < snapshots.Count; i++)
            {
                var bundle = snapshots[i];
                if (bundle.TrainId == Guid.Empty)
                {
                    continue;
                }

                var unit = new ClientTrainUnit(bundle.TrainId, _railGraphProvider);
                unit.SnapshotUpdate(bundle.Simulation, bundle.Diagram, bundle.RailPositionSnapshot, serverTick);
                _units[bundle.TrainId] = unit;
            }

            LastServerTick = serverTick;
        }

        // 最終Tickだけを更新する
        // Override only the latest tick marker
        public void OverrideTick(long serverTick)
        {
            LastServerTick = Math.Max(LastServerTick, serverTick);
        }

        // 現在のTrainUnit状態からハッシュを計算する
        // Compute a hash from the current train unit cache
        public uint ComputeCurrentHash()
        {
            var bundles = new List<TrainUnitSnapshotBundle>(_units.Count);
            foreach (var unit in _units.Values)
            {
                if (!unit.TryCreateSnapshotBundle(out var bundle))
                {
                    continue;
                }
                bundles.Add(bundle);
            }
            return TrainUnitSnapshotHashCalculator.Compute(bundles);
        }

        // 単一列車の差分更新を適用
        // Apply a diff snapshot for a single train
        public ClientTrainUnit Upsert(TrainUnitSnapshotBundle snapshot, long serverTick)
        {
            if (!_units.TryGetValue(snapshot.TrainId, out var unit))
            {
                unit = new ClientTrainUnit(snapshot.TrainId, _railGraphProvider);
                _units[snapshot.TrainId] = unit;
            }

            unit.SnapshotUpdate(snapshot.Simulation, snapshot.Diagram, snapshot.RailPositionSnapshot, serverTick);
            LastServerTick = Math.Max(LastServerTick, serverTick);
            return unit;
        }

        // キャッシュから列車を削除
        // Remove a train from the cache
        public bool Remove(Guid trainId)
        {
            return _units.Remove(trainId);
        }

        // 列車情報の取得を試みる
        // Try retrieving the train info
        public bool TryGet(Guid trainId, out ClientTrainUnit unit)
        {
            return _units.TryGetValue(trainId, out unit);
        }

        internal void CopyUnitsTo(List<ClientTrainUnit> buffer)
        {
            buffer.Clear();
            buffer.AddRange(_units.Values);
        }

        public void ApplyDiagramEvent(TrainDiagramEventMessagePack message)
        {
            if (message == null)
            {
                return;
            }

            if (_units.TryGetValue(message.TrainId, out var unit))
            {
                unit.ApplyDiagramEvent(message);
                var localHash = TrainDiagramHashCalculator.Compute(unit.Diagram.Snapshot);
                if (localHash != message.DiagramHash)
                {
                    Debug.LogWarning($"[TrainDiagramHashVerifier] Hash mismatch for train={message.TrainId}. client={localHash}, server={message.DiagramHash}, tick={message.Tick}, event={message.EventType}.");
                }
            }
        }
    }
}
