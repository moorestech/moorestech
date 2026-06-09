using System.Collections.Generic;
using Client.Game.InGame.Train.Unit;
using Client.Game.InGame.Train.View.Object;
using Core.Master;
using Game.Train.RailPositions;
using Game.Train.Unit;

namespace Client.Game.InGame.Train.View
{
    public sealed class TrainUnitVisualUpdater
    {
        private readonly TrainUnitClientCache _trainCache;
        private readonly TrainCarObjectDatastore _trainCarDatastore;
        private readonly Dictionary<TrainUnitInstanceId, TrainUnitRenderHistory> _historiesByUnit = new();
        private readonly HashSet<TrainUnitInstanceId> _activeUnitIds = new();
        private readonly List<TrainUnitInstanceId> _removeUnitIds = new();

        public TrainUnitVisualUpdater(TrainUnitClientCache trainCache, TrainCarObjectDatastore trainCarDatastore)
        {
            // TrainUnit を構成境界として、描画更新に必要な cache と entity 索引を保持する
            // Keep the train cache and entity index needed to update visuals at the TrainUnit boundary
            _trainCache = trainCache;
            _trainCarDatastore = trainCarDatastore;
        }

        public void UpdateAll(double renderTick)
        {
            // 現在存在する TrainUnit だけを一括更新し、消えた unit の履歴を後で掃除する
            // Update only active TrainUnits and clean histories for removed units afterward
            _activeUnitIds.Clear();
            foreach (var pair in _trainCache.Units)
            {
                var unit = pair.Value;
                _activeUnitIds.Add(unit.TrainUnitInstanceId);
                UpdateUnitVisual(unit, renderTick);
            }
            RemoveInactiveHistories();
        }

        private void UpdateUnitVisual(ClientTrainUnit unit, double renderTick)
        {
            if (!TryCreateCurrentSnapshot(unit, renderTick, out var currentSnapshot))
            {
                ApplyUnavailableToUnit(unit);
                _historiesByUnit.Remove(unit.TrainUnitInstanceId);
                return;
            }

            // unit 単位で旧新 snapshot を保持し、補間用 snapshot を選ぶ
            // Keep previous/current snapshots per unit and select the snapshot used for interpolation
            var history = ResolveHistory(unit.TrainUnitInstanceId);
            var renderSnapshot = history.PushAndResolve(currentSnapshot);
            ApplySnapshot(renderSnapshot);
        }

        private bool TryCreateCurrentSnapshot(ClientTrainUnit unit, double renderTick, out TrainUnitRenderSnapshot snapshot)
        {
            snapshot = default;
            if (unit.RailPosition == null)
            {
                return false;
            }

            // RailPosition は mutable なので描画履歴用に必ず snapshot 化する
            // RailPosition is mutable, so always snapshot it for render history
            var cars = CopyCars(unit.Cars);
            snapshot = TrainUnitRenderSnapshot.Create(
                renderTick,
                unit.RailPosition.DeepCopy(),
                cars,
                unit.CurrentSpeed,
                unit.MasconLevel);
            return true;
        }

        private TrainUnitRenderHistory ResolveHistory(TrainUnitInstanceId trainUnitInstanceId)
        {
            if (_historiesByUnit.TryGetValue(trainUnitInstanceId, out var history))
            {
                return history;
            }

            // 新規 unit には履歴 container を作り、初回は current snapshot だけで描画する
            // Create a history container for new units; the first render uses only the current snapshot
            history = new TrainUnitRenderHistory();
            _historiesByUnit[trainUnitInstanceId] = history;
            return history;
        }

        private void ApplySnapshot(TrainUnitRenderSnapshot snapshot)
        {
            var context = TrainCarContext.CreateAvailable(snapshot.CurrentSpeed, snapshot.MasconLevel);
            var offsetFromHead = 0;
            for (var i = 0; i < snapshot.Cars.Count; i++)
            {
                var car = snapshot.Cars[i];
                var carLength = ResolveCarLength(car);
                if (carLength <= 0)
                {
                    continue;
                }

                // 選択された unit snapshot の RailPosition と car offset から各 car の pose を更新する
                // Update each car pose from the selected unit snapshot RailPosition and car offsets
                var frontOffset = offsetFromHead;
                var rearOffset = offsetFromHead + carLength;
                offsetFromHead = rearOffset;
                if (!_trainCarDatastore.TryGetEntity(car.TrainCarInstanceId, out var entity))
                {
                    continue;
                }

                var visualState = TrainCarRailPositionVisualState.Create(
                    snapshot.RailPosition,
                    frontOffset,
                    rearOffset,
                    car.IsFacingForward);
                entity.ApplyVisualState(visualState, context);
            }
        }

        private void ApplyUnavailableToUnit(ClientTrainUnit unit)
        {
            // unit snapshot が無効な時は対象 car processor へ unavailable を配る
            // Dispatch unavailable state to car processors when the unit snapshot is invalid
            var cars = unit.Cars;
            for (var i = 0; i < cars.Count; i++)
            {
                if (!_trainCarDatastore.TryGetEntity(cars[i].TrainCarInstanceId, out var entity))
                {
                    continue;
                }
                entity.ApplyUnavailableVisualState();
            }
        }

        private void RemoveInactiveHistories()
        {
            _removeUnitIds.Clear();
            foreach (var unitId in _historiesByUnit.Keys)
            {
                if (!_activeUnitIds.Contains(unitId))
                {
                    _removeUnitIds.Add(unitId);
                }
            }

            // cache から消えた TrainUnit の補間履歴を破棄する
            // Remove interpolation histories for TrainUnits no longer present in the cache
            for (var i = 0; i < _removeUnitIds.Count; i++)
            {
                _historiesByUnit.Remove(_removeUnitIds[i]);
            }
        }

        private static TrainCarSnapshot[] CopyCars(IReadOnlyList<TrainCarSnapshot> cars)
        {
            var copiedCars = new TrainCarSnapshot[cars.Count];
            for (var i = 0; i < cars.Count; i++)
            {
                copiedCars[i] = cars[i];
            }
            return copiedCars;
        }

        private static int ResolveCarLength(TrainCarSnapshot snapshot)
        {
            // マスタ情報から車両長を rail unit へ変換する
            // Convert the master car length into rail units
            if (MasterHolder.TrainUnitMaster.TryGetTrainCarMaster(snapshot.TrainCarMasterId, out var master) && master.Length > 0)
            {
                return TrainLengthConverter.ToRailUnits(master.Length);
            }
            return 0;
        }

        private sealed class TrainUnitRenderHistory
        {
            private bool _hasPrevious;
            private bool _hasCurrent;
            private TrainUnitRenderSnapshot _previous;
            private TrainUnitRenderSnapshot _current;

            public TrainUnitRenderSnapshot PushAndResolve(TrainUnitRenderSnapshot next)
            {
                if (_hasCurrent)
                {
                    _previous = _current;
                    _hasPrevious = true;
                }
                _current = next;
                _hasCurrent = true;

                // TODO: ここで renderTick と旧新 snapshot tick から補間 RailPosition を作る
                // TODO: Build an interpolated RailPosition here from renderTick and previous/current snapshot ticks
                if (!_hasPrevious || !CanUsePreviousSnapshot(_previous, _current))
                {
                    return _current;
                }
                return _previous;
            }

            private static bool CanUsePreviousSnapshot(TrainUnitRenderSnapshot previous, TrainUnitRenderSnapshot current)
            {
                if (current.Tick <= previous.Tick)
                {
                    return false;
                }
                if (current.Cars.Count != previous.Cars.Count)
                {
                    return false;
                }

                // car 構成が変わった時は旧 snapshot を補間元として使わない
                // Do not use the previous snapshot as an interpolation source when car composition changed
                for (var i = 0; i < current.Cars.Count; i++)
                {
                    if (current.Cars[i].TrainCarInstanceId != previous.Cars[i].TrainCarInstanceId)
                    {
                        return false;
                    }
                }
                return true;
            }
        }

        private readonly struct TrainUnitRenderSnapshot
        {
            public double Tick { get; }
            public RailPosition RailPosition { get; }
            public IReadOnlyList<TrainCarSnapshot> Cars { get; }
            public double CurrentSpeed { get; }
            public int MasconLevel { get; }

            private TrainUnitRenderSnapshot(double tick, RailPosition railPosition, IReadOnlyList<TrainCarSnapshot> cars, double currentSpeed, int masconLevel)
            {
                Tick = tick;
                RailPosition = railPosition;
                Cars = cars;
                CurrentSpeed = currentSpeed;
                MasconLevel = masconLevel;
            }

            public static TrainUnitRenderSnapshot Create(double tick, RailPosition railPosition, IReadOnlyList<TrainCarSnapshot> cars, double currentSpeed, int masconLevel)
            {
                return new TrainUnitRenderSnapshot(tick, railPosition, cars, currentSpeed, masconLevel);
            }
        }
    }
}
