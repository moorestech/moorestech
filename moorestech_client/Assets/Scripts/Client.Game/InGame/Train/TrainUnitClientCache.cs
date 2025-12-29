using Game.Train.Train;
using System;
using System.Collections.Generic;

namespace Client.Game.InGame.Train
{
    // RailGraphのキャッシュと同じように列車の状態を保持する
    // Cache that mirrors every train unit similar to the RailGraph cache
    public sealed class TrainUnitClientCache
    {
        // ローカルで追跡する列車一覧
        // Internal dictionary holding every tracked train
        private readonly Dictionary<Guid, ClientTrainUnit> _units = new();

        // 最新の適用済みtick
        // Latest tick that has been fully applied
        public long LastServerTick { get; private set; }

        // 列車一覧の読み取り専用ビュー
        // Read-only view for external systems
        public IReadOnlyDictionary<Guid, ClientTrainUnit> Units => _units;

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

                var unit = new ClientTrainUnit(bundle.TrainId);
                unit.Update(bundle.Simulation, bundle.Diagram, bundle.RailPositionSnapshot, serverTick);
                _units[bundle.TrainId] = unit;
            }

            LastServerTick = serverTick;
        }

        // 単一列車の差分更新を適用
        // Apply a diff snapshot for a single train
        public ClientTrainUnit Upsert(TrainUnitSnapshotBundle snapshot, long serverTick)
        {
            if (!_units.TryGetValue(snapshot.TrainId, out var unit))
            {
                unit = new ClientTrainUnit(snapshot.TrainId);
                _units[snapshot.TrainId] = unit;
            }

            unit.Update(snapshot.Simulation, snapshot.Diagram, snapshot.RailPositionSnapshot, serverTick);
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
    }

    // クライアント上で扱う最小限の列車データ
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
        public RailPositionSaveData RailPosition { get; private set; }
        public long LastUpdatedTick { get; private set; }

        // スナップショットの内容で内部状態を更新
        // Update internal state by the received snapshot
        public void Update(TrainSimulationSnapshot simulation, TrainDiagramSnapshot diagram, RailPositionSaveData railPosition, long tick)
        {
            Simulation = simulation;
            Diagram = diagram;
            RailPosition = railPosition;
            LastUpdatedTick = tick;
        }
    }
}
