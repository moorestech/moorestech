using System;
using System.Collections.Generic;
using Client.Game.InGame.Entity;
using Client.Game.InGame.Entity.Object;
using Core.Master;
using Game.Train.Utility;
using UnityEngine;
using VContainer.Unity;

namespace Client.Game.InGame.Train
{
    public sealed class TrainCarEntityPoseUpdater : ITickable
    {
        private readonly TrainUnitClientCache _trainCache;
        private readonly EntityObjectDatastore _entityDatastore;
        private readonly TrainCarPoseCalculator _poseCalculator;
        private readonly List<ClientTrainUnit> _units = new();
        private readonly List<TrainCarEntityObject> _trainCars = new();
        private readonly Dictionary<Guid, TrainCarEntityObject> _carLookup = new();
        private readonly HashSet<Guid> _missingRailPositionLogged = new();
        private readonly HashSet<Guid> _missingCarSnapshotsLogged = new();
        private readonly HashSet<Guid> _missingEntitiesLogged = new();
        private readonly HashSet<Guid> _missingLengthsLogged = new();
        private readonly HashSet<Guid> _poseFailureLogged = new();
        public TrainCarEntityPoseUpdater(TrainUnitClientCache trainCache, EntityObjectDatastore entityDatastore, TrainCarPoseCalculator poseCalculator)
        {
            _trainCache = trainCache;
            _entityDatastore = entityDatastore;
            _poseCalculator = poseCalculator;
        }

        public void Tick()
        {
            // 列車エンチE��チE��と列車キャチE��ュを集める
            // Collect train entities and train unit cache
            _entityDatastore.CopyTrainCarEntitiesTo(_trainCars);
            if (_trainCars.Count == 0) return;
            _trainCache.CopyUnitsTo(_units);
            if (_units.Count == 0) return;

            // 車両IDからエンチE��チE��への索引を作る
            // Build lookup from car id to entity
            BuildCarLookup();
            for (var i = 0; i < _units.Count; i++)
            {
                UpdateUnitPose(_units[i]);
            }
        }

        #region Internal

        private void BuildCarLookup()
        {
            _carLookup.Clear();
            for (var i = 0; i < _trainCars.Count; i++)
            {
                var car = _trainCars[i];
                if (car == null) continue;
                _carLookup[car.TrainCarId] = car;
            }
        }

        private void UpdateUnitPose(ClientTrainUnit unit)
        {
            // 列車のRailPositionと車両リストを確認する
            // Validate rail position and car list
            var railPosition = unit.RailPosition;
            if (railPosition == null)
            {
                LogMissingRailPosition(unit);
                return;
            }
            var carSnapshots = unit.Cars;
            if (carSnapshots == null || carSnapshots.Count == 0)
            {
                LogMissingCarSnapshots(unit);
                return;
            }

            // 先頭からの距離を積み上げて車両の姿勢を更新する
            // Accumulate distance from head and update car poses
            var offsetFromHead = 0;
            for (var i = 0; i < carSnapshots.Count; i++)
            {
                var carSnapshot = carSnapshots[i];
                var carLength = ResolveCarLength(carSnapshot, _carLookup.TryGetValue(carSnapshot.CarId, out var trainCarEntity) ? trainCarEntity : null);
                if (carLength <= 0)
                {
                    LogMissingLength(carSnapshot);
                    continue;
                }
                var frontOffset = offsetFromHead;
                var rearOffset = offsetFromHead + carLength;
                if (trainCarEntity == null)
                {
                    LogMissingEntity(carSnapshot);
                    offsetFromHead += carLength;
                    continue;
                }
                if (!TryResolveCarPose(railPosition, frontOffset, rearOffset, out var position, out var forward))
                {
                    LogPoseFailure(carSnapshot, frontOffset, rearOffset);
                    offsetFromHead += carLength;
                    continue;
                }
                var rotation = BuildRotation(forward, carSnapshot.IsFacingForward);
                trainCarEntity.SetPoseWithLerp(position, rotation);
                offsetFromHead += carLength;
            }
        }

        private int ResolveCarLength(TrainCarSnapshot snapshot, TrainCarEntityObject trainCarEntity)
        {
            // マスター長さをrail単位に変換する
            // Convert master length into rail units
            var master = trainCarEntity?.TrainCarMasterElement;
            if (master != null && master.Length > 0) return TrainLengthConverter.ToRailUnits(master.Length);
            if (MasterHolder.TrainUnitMaster.TryGetTrainUnit(snapshot.TrainCarGuid, out var fallbackMaster) && fallbackMaster.Length > 0) return TrainLengthConverter.ToRailUnits(fallbackMaster.Length);
            return 0;
        }

        private bool TryResolveCarPose(RailPosition railPosition, int frontOffset, int rearOffset, out Vector3 position, out Vector3 forward)
        {
            // 前輪と後輪の位置から車両姿勢を算出する
            // Compute the car pose from front and rear wheel positions
            position = default;
            forward = Vector3.forward;
            if (!_poseCalculator.TryGetPose(railPosition, frontOffset, out var frontPosition, out var frontForward)) return false;
            if (!_poseCalculator.TryGetPose(railPosition, rearOffset, out var rearPosition, out _)) return false;
            position = (frontPosition + rearPosition) * 0.5f;
            var delta = frontPosition - rearPosition;
            forward = delta.sqrMagnitude > 1e-6f ? delta.normalized : (frontForward.sqrMagnitude > 1e-6f ? frontForward.normalized : Vector3.forward);
            return true;
        }

        private Quaternion BuildRotation(Vector3 forward, bool isFacingForward)
        {
            // 正規化した向きから回転を作る
            // Build rotation from normalized forward vector
            var safeForward = forward.sqrMagnitude > 1e-6f ? forward.normalized : Vector3.forward;
            var rotation = Quaternion.LookRotation(safeForward, Vector3.up);
            if (!isFacingForward) rotation = rotation * Quaternion.Euler(0f, 180f, 0f);
            return rotation;
        }

        private void LogMissingRailPosition(ClientTrainUnit unit)
        {
            // RailPositionが未復元の編成を一度だけ警告する
            // Warn once when RailPosition is missing for a train
            if (_missingRailPositionLogged.Add(unit.TrainId)) Debug.LogWarning($"[TrainCarPose] RailPosition missing for train={unit.TrainId}.");
        }

        private void LogMissingCarSnapshots(ClientTrainUnit unit)
        {
            // 車両スナップショットが空の編成を一度だけ警告する
            // Warn once when car snapshots are empty
            if (_missingCarSnapshotsLogged.Add(unit.TrainId)) Debug.LogWarning($"[TrainCarPose] Car snapshots missing for train={unit.TrainId}.");
        }

        private void LogMissingEntity(TrainCarSnapshot snapshot)
        {
            // 車両エンティティ未生成を一度だけ警告する
            // Warn once when the train car entity is missing
            if (_missingEntitiesLogged.Add(snapshot.CarId)) Debug.LogWarning($"[TrainCarPose] Entity missing for car={snapshot.CarId}.");
        }

        private void LogMissingLength(TrainCarSnapshot snapshot)
        {
            // 車両長が解決できない場合に一度だけ警告する
            // Warn once when car length cannot be resolved
            if (_missingLengthsLogged.Add(snapshot.CarId)) Debug.LogWarning($"[TrainCarPose] Car length missing for car={snapshot.CarId}, master={snapshot.TrainCarGuid}.");
        }

        private void LogPoseFailure(TrainCarSnapshot snapshot, int frontOffset, int rearOffset)
        {
            // 車両姿勢計算の失敗を一度だけ警告する
            // Warn once when pose calculation fails
            if (_poseFailureLogged.Add(snapshot.CarId)) Debug.LogWarning($"[TrainCarPose] Pose resolve failed for car={snapshot.CarId}, frontOffset={frontOffset}, rearOffset={rearOffset}.");
        }

        #endregion
    }
}
