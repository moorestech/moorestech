using Game.Train.Train;
using System;
using System.Collections.Generic;

namespace Client.Game.InGame.Train
{
    // RailGraphのキャッシュと同様に列車全体を管理するクラス
    // Cache that mirrors every train unit similar to RailGraphClientCache
    public sealed class TrainUnitClientCache
    {
        // クライアントで管理する列車一覧
        // Internal dictionary holding every tracked train
        private readonly Dictionary<Guid, ClientTrainUnit> _units = new();

        // 最新同期済みtick
        // Latest tick that has been fully applied
        public long LastServerTick { get; private set; }

        // 列車一覧の読み取り専用ビュー
        // Read-only view for external systems
        public IReadOnlyDictionary<Guid, ClientTrainUnit> Units => _units;

        // フルスナップショット受信時に全キャッシュを置き換える
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
                var unit = new ClientTrainUnit(bundle.TrainId);
                unit.Update(bundle.Simulation, bundle.Diagram, serverTick);
                _units[bundle.TrainId] = unit;
            }
            LastServerTick = serverTick;
        }

        // 単一列車の差分スナップショットを適用
        // Apply a diff snapshot for a single train
        public ClientTrainUnit Upsert(TrainUnitSnapshotBundle snapshot, long serverTick)
        {
            if (!_units.TryGetValue(snapshot.TrainId, out var unit))
            {
                unit = new ClientTrainUnit(snapshot.TrainId);
                _units[snapshot.TrainId] = unit;
            }
            unit.Update(snapshot.Simulation, snapshot.Diagram, serverTick);
            LastServerTick = Math.Max(LastServerTick, serverTick);
            return unit;
        }

        // キャッシュから列車を削除
        // Remove a train from the cache
        public bool Remove(Guid trainId)
        {
            return _units.Remove(trainId);
        }

        // 列車情報の取得を試行
        // Try retrieving the train info
        public bool TryGet(Guid trainId, out ClientTrainUnit unit)
        {
            return _units.TryGetValue(trainId, out unit);
        }
    }

    // クライアント上で扱う列車データの最小構成
    // Minimal client-side representation of a train
    public sealed class ClientTrainUnit
    {
        public ClientTrainUnit(Guid trainId)
        {
            TrainId = trainId;
        }

        public Guid TrainId { get; }
        public TrainSimulationSnapshot Simulation { get; private set; }
        public TrainDiagramSnapshot Diagram { get; private set; }
        public long LastUpdatedTick { get; private set; }

        // 受信したスナップショットで状態を更新
        // Update internal state by the received snapshot
        public void Update(TrainSimulationSnapshot simulation, TrainDiagramSnapshot diagram, long tick)
        {
            Simulation = simulation;
            Diagram = diagram;
            LastUpdatedTick = tick;
        }
    }
}
