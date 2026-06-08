using System.Collections.Generic;
using Client.Game.InGame.Train.Unit;
using Client.Game.InGame.Train.View.Object;
using Core.Master;
using Game.Train.Unit;

namespace Client.Game.InGame.Train.View
{
    public sealed class TrainCarEntityRenderInterpolator
    {
        private readonly TrainUnitTickState _tickState;
        private readonly TrainCarInstanceId _trainCarInstanceId;
        private readonly ClientTrainUnit _trainUnit;
        private readonly ITrainCarVisualTarget _visualTarget;
        private readonly ITrainCarObjectProcessor[] _processors;
        private IReadOnlyList<TrainCarSnapshot> _cachedCars;
        private TrainCarSnapshot _cachedSnapshot;
        private int _cachedFrontOffset;
        private int _cachedRearOffset;

        public TrainCarEntityRenderInterpolator(
            TrainCarEntityObject trainCarEntity,
            ITrainCarVisualTarget visualTarget,
            TrainUnitClientCache trainCache,
            TrainUnitTickState tickState)
        {
            // 通常描画modeに必要な固定依存だけを保持する
            // Keep only fixed dependencies required by runtime render mode
            _tickState = tickState;
            _trainCarInstanceId = trainCarEntity.TrainCarInstanceId;
            _visualTarget = visualTarget;
            trainCache.TryGetCarSnapshot(_trainCarInstanceId, out _trainUnit, out _, out _, out _);

            // animationなど通常描画専用processorはentity生成時に初期化する
            // Initialize runtime-only processors such as animation at entity creation
            _processors = trainCarEntity.GetComponentsInChildren<ITrainCarObjectProcessor>();
            for (var i = 0; i < _processors.Length; i++)
            {
                _processors[i].Initialize(trainCarEntity);
            }
        }

        public void Update()
        {
            // cached unitから最新の描画snapshotを作る
            // Build the latest render snapshot from the cached unit
            if (!TryResolveLatestRenderSnapshot(_tickState.GetTick(), out var renderSnapshot))
            {
                DispatchProcessors(TrainCarContext.CreateUnavailable());
                return;
            }

            // railposition描画入口へ渡し、失敗時はprocessorにも利用不可を伝える
            // Apply through the railposition visual entry and mark processors unavailable on failure
            var visualState = TrainCarRailPositionVisualState.CreateFromRenderSnapshot(renderSnapshot);
            if (!_visualTarget.UpdateVisual(visualState))
            {
                DispatchProcessors(TrainCarContext.CreateUnavailable());
                return;
            }

            DispatchProcessors(CreateContext(renderSnapshot));
        }

        public void DestroyRuntimeResources()
        {
            // visual targetが持つruntime materialを解放する
            // Release runtime materials owned by the visual target
            _visualTarget.DestroyRuntimeMaterials();
        }

        private static TrainCarContext CreateContext(TrainCarRenderSnapshot renderSnapshot)
        {
            // processor用contextは描画に使ったsnapshotから作る
            // Build processor context from the snapshot used for rendering
            return TrainCarContext.CreateAvailable(renderSnapshot.CurrentSpeed, renderSnapshot.MasconLevel);
        }

        private void DispatchProcessors(TrainCarContext context)
        {
            // 通常描画専用processorへ同じcontextを配る
            // Dispatch the same context to all runtime-only processors
            for (var i = 0; i < _processors.Length; i++)
            {
                _processors[i].Update(context);
            }
        }

        private bool TryResolveLatestRenderSnapshot(uint tick, out TrainCarRenderSnapshot renderSnapshot)
        {
            renderSnapshot = default;
            if (_trainUnit == null)
            {
                return false;
            }

            var railPosition = _trainUnit.RailPosition;
            if (railPosition == null)
            {
                return false;
            }

            // car listが更新された時だけ対象carのsnapshotとoffsetを再解決する
            // Re-resolve the target car snapshot and offsets only when the car list changes
            if (!TryRefreshCachedCarState())
            {
                return false;
            }

            // 現段階では補間履歴を持たず、cached unit上の最新RailPositionを使う
            // This pass has no interpolation history and uses the latest RailPosition on the cached unit
            renderSnapshot = TrainCarRenderSnapshot.Create(
                tick,
                railPosition,
                _cachedFrontOffset,
                _cachedRearOffset,
                _cachedSnapshot.IsFacingForward,
                _trainUnit.CurrentSpeed,
                _trainUnit.MasconLevel);
            return true;
        }

        private bool TryRefreshCachedCarState()
        {
            var cars = _trainUnit.Cars;
            if (ReferenceEquals(cars, _cachedCars))
            {
                return true;
            }

            _cachedCars = cars;
            var offsetFromHead = 0;
            for (var i = 0; i < cars.Count; i++)
            {
                var snapshot = cars[i];
                var carLength = ResolveCarLength(snapshot);
                if (carLength <= 0)
                {
                    continue;
                }

                var frontOffset = offsetFromHead;
                var rearOffset = offsetFromHead + carLength;
                offsetFromHead = rearOffset;
                if (snapshot.TrainCarInstanceId != _trainCarInstanceId)
                {
                    continue;
                }

                // 対象carを見つけた時だけcacheを確定する
                // Commit the cache only after the target car is found
                _cachedCars = cars;
                _cachedSnapshot = snapshot;
                _cachedFrontOffset = frontOffset;
                _cachedRearOffset = rearOffset;
                return true;
            }
            _cachedCars = null;
            _cachedSnapshot = default;
            _cachedFrontOffset = 0;
            _cachedRearOffset = 0;
            return false;
        }

        private static int ResolveCarLength(TrainCarSnapshot snapshot)
        {
            // マスタ情報から車両長をrail unitへ変換する
            // Convert the master car length into rail units
            if (MasterHolder.TrainUnitMaster.TryGetTrainCarMaster(snapshot.TrainCarMasterId, out var master) && master.Length > 0)
            {
                return TrainLengthConverter.ToRailUnits(master.Length);
            }
            return 0;
        }
    }
}
