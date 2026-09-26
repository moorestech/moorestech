using System.Collections.Generic;
using Core.Master;
using Game.Train.Unit;

namespace Client.Game.InGame.Train.Unit
{
    // 車両の所属と編成内オフセットを索引化する
    // Index car ownership and offsets within each train
    internal sealed class TrainCarSnapshotIndex
    {
        private readonly Dictionary<TrainCarInstanceId, TrainCarCacheEntry> _carIndex = new();
        private readonly Dictionary<TrainUnitInstanceId, List<TrainCarInstanceId>> _carIdsByTrain = new();

        public void Clear()
        {
            _carIndex.Clear();
            _carIdsByTrain.Clear();
        }

        public bool TryGetCarSnapshot(TrainCarInstanceId trainCarInstanceId, out ClientTrainUnit unit, out TrainCarSnapshot snapshot, out int frontOffset, out int rearOffset)
        {
            // 出力を初期化する
            // Initialize output values
            unit = null;
            snapshot = default;
            frontOffset = 0;
            rearOffset = 0;

            // 索引から対象車両を取得する
            // Lookup the target car from the index
            if (!_carIndex.TryGetValue(trainCarInstanceId, out var entry)) return false;
            unit = entry.Unit;
            snapshot = entry.Snapshot;
            frontOffset = entry.FrontOffset;
            rearOffset = entry.RearOffset;
            return true;
        }

        public void BuildCarIndexForUnit(ClientTrainUnit unit)
        {
            // 車両スナップショットから索引を構築する
            // Build car index entries from snapshots
            var cars = unit.Cars;
            if (cars.Count == 0) return;

            var carIds = new List<TrainCarInstanceId>(cars.Count);
            var offsetFromHead = 0;
            for (var i = 0; i < cars.Count; i++)
            {
                // 車両長さを算出し前後オフセットを登録する
                // Resolve length and store front/rear offsets
                var carSnapshot = cars[i];
                var carLength = ResolveCarLength(carSnapshot);
                if (carLength <= 0) continue;
                var frontOffset = offsetFromHead;
                var rearOffset = offsetFromHead + carLength;
                offsetFromHead += carLength;
                _carIndex[carSnapshot.TrainCarInstanceId] = new TrainCarCacheEntry(unit, carSnapshot, frontOffset, rearOffset);
                carIds.Add(carSnapshot.TrainCarInstanceId);
            }

            _carIdsByTrain[unit.TrainUnitInstanceId] = carIds;

            #region Internal

            int ResolveCarLength(TrainCarSnapshot snapshot)
            {
                // マスター情報から車両長さを解決する
                // Resolve car length from master data
                if (MasterHolder.TrainUnitMaster.TryGetTrainCarMaster(snapshot.TrainCarMasterId, out var master) && 0 < master.Length) return TrainLengthConverter.ToRailUnits(master.Length);
                return 0;
            }

            #endregion
        }

        public void RemoveCarIndex(TrainUnitInstanceId trainUnitInstanceId)
        {
            // 列車に紐づく車両索引を削除する
            // Remove car index entries for the target train
            if (!_carIdsByTrain.TryGetValue(trainUnitInstanceId, out var carIds)) return;
            for (var i = 0; i < carIds.Count; i++) _carIndex.Remove(carIds[i]);
            _carIdsByTrain.Remove(trainUnitInstanceId);
        }
        
        private readonly struct TrainCarCacheEntry
        {
            public readonly ClientTrainUnit Unit;
            public readonly TrainCarSnapshot Snapshot;
            public readonly int FrontOffset;
            public readonly int RearOffset;
            
            public TrainCarCacheEntry(ClientTrainUnit unit, TrainCarSnapshot snapshot, int frontOffset, int rearOffset)
            {
                // 索引の内容を初期化する
                // Initialize entry values
                Unit = unit;
                Snapshot = snapshot;
                FrontOffset = frontOffset;
                RearOffset = rearOffset;
            }
        }
    }
}
