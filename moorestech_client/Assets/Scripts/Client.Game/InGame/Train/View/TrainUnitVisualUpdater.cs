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
        private readonly TrainCarObjectDatastore _trainCarDatastore;

        private bool _hasPrevious;
        private bool _hasCurrent;
        private TrainUnitRenderSnapshot _previous;
        private TrainUnitRenderSnapshot _current;

        public TrainUnitVisualUpdater(TrainCarObjectDatastore trainCarDatastore)
        {
            // car object の実体解決だけを受け取り、unit 管理は上位 system に任せる
            // Receive only car object resolution and leave unit management to the upper system
            _trainCarDatastore = trainCarDatastore;
        }

        public void Update(ClientTrainUnit unit, double renderTick)
        {
            if (!TryCreateCurrentSnapshot(unit, renderTick, out var currentSnapshot))
            {
                // RailPosition が無効な unit は描画不可として扱い、補間履歴も破棄する
                // Treat units with invalid RailPosition as unavailable and discard interpolation history
                ApplyUnavailableToUnit(unit);
                ClearHistory();
                return;
            }

            // unit 単位で保持した旧新 snapshot から、今回描画に使う snapshot を選ぶ
            // Select the snapshot to render from the previous/current snapshots owned by this unit updater
            var renderSnapshot = PushAndResolve(currentSnapshot);
            ApplySnapshot(renderSnapshot);
        }

        private bool TryCreateCurrentSnapshot(ClientTrainUnit unit, double renderTick, out TrainUnitRenderSnapshot snapshot)
        {
            snapshot = default;
            if (unit.RailPosition == null)
            {
                return false;
            }

            // RailPosition は mutable なので、描画履歴用に必ず DeepCopy で固定する
            // RailPosition is mutable, so always freeze it with DeepCopy for render history
            var cars = CopyCars(unit.Cars);
            snapshot = TrainUnitRenderSnapshot.Create(
                renderTick,
                unit.RailPosition.DeepCopy(),
                cars,
                unit.CurrentSpeed,
                unit.MasconLevel);
            return true;
        }

        private TrainUnitRenderSnapshot PushAndResolve(TrainUnitRenderSnapshot next)
        {
            if (_hasCurrent)
            {
                _previous = _current;
                _hasPrevious = true;
            }

            // 最新 snapshot を current に進め、補間できない場合は current をそのまま使う
            // Advance the latest snapshot to current and use current directly when interpolation is not possible
            _current = next;
            _hasCurrent = true;
            if (!_hasPrevious || !CanUsePreviousSnapshot(_previous, _current))
            {
                return _current;
            }

            // TODO: renderTick と旧新 snapshot tick から補間 RailPosition を作る
            // TODO: Build an interpolated RailPosition from renderTick and previous/current snapshot ticks
            return _previous;
        }

        private void ClearHistory()
        {
            // 無効状態から復帰した時に古い snapshot を補間元へ混ぜない
            // Do not mix stale snapshots into interpolation after recovering from unavailable state
            _hasPrevious = false;
            _hasCurrent = false;
            _previous = default;
            _current = default;
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

                // unit の RailPosition と car offset から、対象 car の pose を更新する
                // Update the target car pose from the unit RailPosition and car offsets
                var frontOffset = offsetFromHead;
                var rearOffset = offsetFromHead + carLength;
                offsetFromHead = rearOffset;
                if (!_trainCarDatastore.TryGetEntity(car.TrainCarInstanceId, out var entity))
                {
                    continue;
                }

                // car object 側の再帰 pose updater へ、表示に必要な span を渡す
                // Pass the render span to the recursive pose updater on the car object side
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
            var cars = unit.Cars;
            for (var i = 0; i < cars.Count; i++)
            {
                // car entity が未生成なら、次の snapshot 更新時に生成されるためここでは飛ばす
                // Skip missing car entities because snapshot updates create them later
                if (!_trainCarDatastore.TryGetEntity(cars[i].TrainCarInstanceId, out var entity))
                {
                    continue;
                }
                entity.ApplyUnavailableVisualState();
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
            // マスタの車両長を rail unit に変換し、無効な定義は描画対象外にする
            // Convert master car length to rail units and exclude invalid definitions from rendering
            if (MasterHolder.TrainUnitMaster.TryGetTrainCarMaster(snapshot.TrainCarMasterId, out var master) && master.Length > 0)
            {
                return TrainLengthConverter.ToRailUnits(master.Length);
            }
            return 0;
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

            // car 構成が変わった時は、古い snapshot を補間元として使わない
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

        private readonly struct TrainUnitRenderSnapshot
        {
            public readonly double Tick;
            public readonly RailPosition RailPosition;
            public readonly IReadOnlyList<TrainCarSnapshot> Cars;
            public readonly double CurrentSpeed;
            public readonly int MasconLevel;

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
