using System;
using System.Collections.Generic;
using Client.Game.InGame.Entity;
using Client.Game.InGame.Entity.Object;
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
            var carSnapshots = unit.Cars;
            if (railPosition == null || carSnapshots == null || carSnapshots.Count == 0) return;

            // 先頭からの距離を積み上げて車両の姿勢を更新する
            // Accumulate distance from head and update car poses
            var offsetFromHead = 0;
            for (var i = 0; i < carSnapshots.Count; i++)
            {
                var carSnapshot = carSnapshots[i];
                var carLength = ResolveCarLength(carSnapshot.CarId);
                if (carLength <= 0)
                {
                    continue;
                }
                var centerOffset = offsetFromHead + carLength / 2;
                if (_carLookup.TryGetValue(carSnapshot.CarId, out var trainCarEntity))
                {
                    if (_poseCalculator.TryGetPose(railPosition, centerOffset, out var position, out var forward))
                    {
                        var rotation = BuildRotation(forward, carSnapshot.IsFacingForward);
                        trainCarEntity.SetPoseWithLerp(position, rotation);
                    }
                }
                offsetFromHead += carLength;
            }
        }

        private int ResolveCarLength(Guid carId)
        {
            // マスター長さをrail単位に変換する
            // Convert master length into rail units
            if (_carLookup.TryGetValue(carId, out var trainCarEntity))
            {
                var master = trainCarEntity.TrainCarMasterElement;
                if (master != null && master.Length > 0) return TrainLengthConverter.ToRailUnits(master.Length);
            }
            return 0;
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

        #endregion
    }
}
