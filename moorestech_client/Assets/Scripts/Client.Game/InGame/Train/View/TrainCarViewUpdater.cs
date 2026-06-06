using System;
using Client.Game.InGame.Train.Unit;
using Client.Game.InGame.Train.View.Object;
using Game.Train.RailPositions;
using Game.Train.Unit;
using UnityEngine;

namespace Client.Game.InGame.Train.View
{
    // プレイヤーの足場追従が同フレームの列車姿勢を読むよう、通常Updateより先に動かす
    // Run before normal Update so player platform follow reads the same-frame train pose
    [DefaultExecutionOrder(-200)]
    public sealed class TrainCarViewUpdater : MonoBehaviour
    {
        private TrainUnitClientCache _trainCache;
        private TrainUnitTickState _tickState;
        private ITrainUnitRenderInterpolationProvider _renderInterpolationProvider;
        private TrainCarEntityObject _trainCarEntity;
        private ITrainCarObjectProcessor[] _processors;
        private TrainCarRenderSnapshot _currentRenderSnapshot;
        private TrainCarRenderSnapshot _previousRenderSnapshot;
        private uint _lastSnapshotTick;
        private bool _hasCurrentRenderSnapshot;
        private bool _hasPreviousRenderSnapshot;
        private bool _isReady;
        private bool _visualPartBuffersInitialized;
        private int _visualPartCount;
        private int[] _visualPartAuthoredLengths;
        private int[] _visualPartNormalizedLengths;
        private TrainCarPartSpan[] _visualPartSpans;
        private TrainCarPoseResult[] _previousVisualPartPoses;
        private TrainCarPoseResult[] _currentVisualPartPoses;
        private TrainCarPoseResult[] _outputVisualPartPoses;

        public void Initialize(TrainCarEntityObject trainCarEntity, TrainUnitClientCache trainCache, TrainUnitTickState tickState, ITrainUnitRenderInterpolationProvider renderInterpolationProvider)
        {
            // 依存参照と表示 processor 一覧を初期化時に保持する
            // Store dependency references and view processors during initialization
            _trainCarEntity = trainCarEntity;
            _trainCache = trainCache;
            _tickState = tickState;
            _renderInterpolationProvider = renderInterpolationProvider;
            _currentRenderSnapshot = default;
            _previousRenderSnapshot = default;
            _lastSnapshotTick = 0;
            _hasCurrentRenderSnapshot = false;
            _hasPreviousRenderSnapshot = false;
            _visualPartBuffersInitialized = false;
            _processors = GetComponentsInChildren<ITrainCarObjectProcessor>();
            for (var i = 0; i < _processors.Length; i++)
            {
                _processors[i].Initialize(trainCarEntity);
            }
            _isReady = true;
        }

        private void Update()
        {
            // 初期化完了後のみ列車表示を更新する
            // Update the train view only after initialization completes
            if (!_isReady)
            {
                return;
            }

            // TrainCarEntityObject の姿勢サービス生成後に分割表示を初期化する
            // Initialize split visuals after TrainCarEntityObject creates the pose service
            if (!_trainCarEntity.IsPoseServiceReady())
            {
                DispatchProcessors(TrainCarContext.CreateUnavailable());
                return;
            }
            EnsureVisualPartBuffersInitialized();

            // cache から今回の render snapshot を更新する
            // Refresh the current render snapshot from the cache
            UpdateCurrentRenderSnapshot();

            // 今回の出力 snapshot を解決して姿勢と context を組み立てる
            // Resolve the output snapshot and build pose plus context
            if (!TryResolveOutputPose(out var position, out var rotation, out var contextSnapshot))
            {
                DispatchProcessors(TrainCarContext.CreateUnavailable());
                return;
            }

            // render snapshot から列車姿勢を計算する
            // Compute the car pose from the render snapshot
            // レール由来の姿勢を GameObject に反映する
            // Apply the rail-derived pose to the GameObject
            _trainCarEntity.SetDirectPose(position, rotation);
            if (!TryApplyOutputVisualPartPoses())
            {
                DispatchProcessors(TrainCarContext.CreateUnavailable());
                return;
            }
            DispatchProcessors(CreateContext(contextSnapshot));
        }

        private void EnsureVisualPartBuffersInitialized()
        {
            // part marker は Prefab 初期化後に一度だけ読む
            // Read part markers once after Prefab initialization
            if (_visualPartBuffersInitialized)
            {
                return;
            }
            InitializeVisualPartBuffers();
            _visualPartBuffersInitialized = true;
        }

        private void InitializeVisualPartBuffers()
        {
            // Prefab marker から part 数と authored 長を一度だけ読む
            // Read part count and authored lengths from Prefab markers once
            _visualPartCount = _trainCarEntity.GetVisualPartCount();
            if (_visualPartCount <= 0)
            {
                _visualPartAuthoredLengths = Array.Empty<int>();
                _visualPartNormalizedLengths = Array.Empty<int>();
                _visualPartSpans = Array.Empty<TrainCarPartSpan>();
                _previousVisualPartPoses = Array.Empty<TrainCarPoseResult>();
                _currentVisualPartPoses = Array.Empty<TrainCarPoseResult>();
                _outputVisualPartPoses = Array.Empty<TrainCarPoseResult>();
                return;
            }

            // per-frame allocation を避けるため part 用 buffer を保持する
            // Keep visual-part buffers to avoid per-frame allocation
            _visualPartAuthoredLengths = new int[_visualPartCount];
            _visualPartNormalizedLengths = new int[_visualPartCount];
            _visualPartSpans = new TrainCarPartSpan[_visualPartCount];
            _previousVisualPartPoses = new TrainCarPoseResult[_visualPartCount];
            _currentVisualPartPoses = new TrainCarPoseResult[_visualPartCount];
            _outputVisualPartPoses = new TrainCarPoseResult[_visualPartCount];
            for (var i = 0; i < _visualPartCount; i++)
            {
                _trainCarEntity.TryGetVisualPartLengthMeters(i, out _visualPartAuthoredLengths[i]);
            }
        }

        private void UpdateCurrentRenderSnapshot()
        {
            var currentTick = _tickState.GetTick();
            if (_hasCurrentRenderSnapshot && currentTick == _lastSnapshotTick)
            {
                return;
            }

            // 最新 snapshot を読めた時だけ current を更新する
            // Update the current snapshot only when the cache lookup succeeds
            if (!TryResolveLatestRenderSnapshot(currentTick, out var latestRenderSnapshot))
            {
                return;
            }

            // 将来の補間用に current を previous へ退避する
            // Preserve the last current snapshot for future interpolation
            if (_hasCurrentRenderSnapshot)
            {
                _previousRenderSnapshot = _currentRenderSnapshot;
                _hasPreviousRenderSnapshot = true;
            }
            else
            {
                _previousRenderSnapshot = latestRenderSnapshot;
                _hasPreviousRenderSnapshot = true;
            }

            // 今回の表示基準となる current snapshot を差し替える
            // Replace the current snapshot used for rendering
            _currentRenderSnapshot = latestRenderSnapshot;
            _hasCurrentRenderSnapshot = true;
            _lastSnapshotTick = latestRenderSnapshot.Tick;
        }

        private bool TryResolveOutputPose(out Vector3 position, out Quaternion rotation, out TrainCarRenderSnapshot contextSnapshot)
        {
            // 補間導入前は current snapshot をそのまま出力する
            // Before interpolation, output the current snapshot as-is
            position = default;
            rotation = Quaternion.identity;
            contextSnapshot = default;
            if (!_hasCurrentRenderSnapshot)
            {
                return false;
            }

            contextSnapshot = _currentRenderSnapshot;
            if (!_hasPreviousRenderSnapshot)
            {
                return TryResolveRenderPose(_currentRenderSnapshot, out position, out rotation);
            }

            if (!TryResolveRenderPose(_previousRenderSnapshot, out var previousPosition, out var previousRotation))
            {
                return false;
            }
            if (!TryResolveRenderPose(_currentRenderSnapshot, out var currentPosition, out var currentRotation))
            {
                return false;
            }

            var baseInterpolationRate = (float)_renderInterpolationProvider.GetRenderInterpolationRate();
            var snapshotTickDelta = _currentRenderSnapshot.Tick >= _previousRenderSnapshot.Tick ? _currentRenderSnapshot.Tick - _previousRenderSnapshot.Tick : 0;
            var interpolationRate = CalculateSnapshotGapInterpolationRate(baseInterpolationRate, snapshotTickDelta);
            position = Vector3.LerpUnclamped(previousPosition, currentPosition, interpolationRate);
            rotation = Quaternion.SlerpUnclamped(previousRotation, currentRotation, interpolationRate);
            return true;
        }

        private bool TryApplyOutputVisualPartPoses()
        {
            // 分割表示がない車両は root pose のみで完了する
            // Cars without split visuals complete with the root pose only
            if (_visualPartCount <= 0)
            {
                return true;
            }
            if (!_hasCurrentRenderSnapshot)
            {
                return false;
            }

            // snapshot pair から part ごとの補間済み pose を作る
            // Build interpolated per-part poses from the snapshot pair
            if (!TryResolveOutputVisualPartPoses())
            {
                return false;
            }
            for (var i = 0; i < _visualPartCount; i++)
            {
                var pose = _outputVisualPartPoses[i];
                _trainCarEntity.SetDirectPartPose(i, pose.Position, pose.Rotation);
            }
            return true;
        }

        private bool TryResolveOutputVisualPartPoses()
        {
            // previous がない初回は current pose をそのまま使う
            // Use the current pose as-is on the first frame without previous data
            if (!_hasPreviousRenderSnapshot)
            {
                return TryResolveVisualPartPoses(_currentRenderSnapshot, _outputVisualPartPoses);
            }
            if (!TryResolveVisualPartPoses(_previousRenderSnapshot, _previousVisualPartPoses))
            {
                return false;
            }
            if (!TryResolveVisualPartPoses(_currentRenderSnapshot, _currentVisualPartPoses))
            {
                return false;
            }

            // root pose と同じ補間率で各 part を補間する
            // Interpolate each part with the same rate used by the root pose
            var baseInterpolationRate = (float)_renderInterpolationProvider.GetRenderInterpolationRate();
            var snapshotTickDelta = _currentRenderSnapshot.Tick >= _previousRenderSnapshot.Tick ? _currentRenderSnapshot.Tick - _previousRenderSnapshot.Tick : 0;
            var interpolationRate = CalculateSnapshotGapInterpolationRate(baseInterpolationRate, snapshotTickDelta);
            for (var i = 0; i < _visualPartCount; i++)
            {
                var previous = _previousVisualPartPoses[i];
                var current = _currentVisualPartPoses[i];
                var position = Vector3.LerpUnclamped(previous.Position, current.Position, interpolationRate);
                var rotation = Quaternion.SlerpUnclamped(previous.Rotation, current.Rotation, interpolationRate);
                _outputVisualPartPoses[i] = new TrainCarPoseResult(position, rotation);
            }
            return true;
        }

        private bool TryResolveVisualPartPoses(TrainCarRenderSnapshot renderSnapshot, TrainCarPoseResult[] outputPoses)
        {
            // 車両長へ part 比率を正規化する
            // Normalize part ratios to the current car length
            var carLength = renderSnapshot.RearOffset - renderSnapshot.FrontOffset;
            if (!TrainCarPartPoseCalculator.TryBuildNormalizedPartLengths(carLength, _visualPartAuthoredLengths, _visualPartNormalizedLengths, out var partCount))
            {
                return false;
            }
            if (partCount != _visualPartCount || outputPoses.Length < partCount)
            {
                return false;
            }

            // model front から順に span を作り、現在の RailPosition 上で姿勢へ変換する
            // Build spans from model front and convert each to a pose on the current RailPosition
            var partStartOffset = 0;
            for (var i = 0; i < partCount; i++)
            {
                var partLength = _visualPartNormalizedLengths[i];
                if (!TrainCarPartPoseCalculator.TryBuildPartSpan(renderSnapshot.FrontOffset, renderSnapshot.RearOffset, partStartOffset, partLength, renderSnapshot.IsFacingForward, out _visualPartSpans[i]))
                {
                    return false;
                }
                if (!_trainCarEntity.TryGetVisualPartModelForwardCenterOffset(i, out var modelForwardCenterOffset))
                {
                    return false;
                }

                // part renderer の中心補正込みで Transform pose を解く
                // Resolve Transform pose including each part renderer-center correction
                var span = _visualPartSpans[i];
                if (!TrainCarPoseCalculator.TryResolveRenderPose(renderSnapshot.RailPosition, span.FrontOffset, span.RearOffset, renderSnapshot.IsFacingForward, modelForwardCenterOffset, out var position, out var rotation))
                {
                    return false;
                }
                outputPoses[i] = new TrainCarPoseResult(position, rotation);
                partStartOffset += partLength;
            }
            return true;
        }

        private static float CalculateSnapshotGapInterpolationRate(float baseInterpolationRate, uint snapshotTickDelta)
        {
            // 欠落snapshotがある時は古いsnapshotからの経過tick量に変換する
            // Convert to elapsed ticks from the older snapshot when intermediate snapshots are missing
            if (snapshotTickDelta < 2)
            {
                return baseInterpolationRate;
            }

            // 例: 2tick差でbase 0.5なら2tick区間内の1.5tick地点として扱う
            // Example: with a 2-tick gap and base 0.5, use the 1.5-tick point inside that range
            return (snapshotTickDelta - 1 + baseInterpolationRate) / snapshotTickDelta;
        }

        private bool TryResolveRenderPose(TrainCarRenderSnapshot renderSnapshot, out Vector3 position, out Quaternion rotation)
        {
            // 出力値を先に初期化する
            // Initialize output values first
            position = default;
            rotation = Quaternion.identity;

            // レール上の前後位置から車両姿勢を計算する
            // Compute rail pose from front and rear wheel positions
            if (!TrainCarPoseCalculator.TryResolveRenderPose(renderSnapshot.RailPosition, renderSnapshot.FrontOffset, renderSnapshot.RearOffset, renderSnapshot.IsFacingForward, _trainCarEntity.ModelForwardCenterOffset, out position, out rotation))
            {
                return false;
            }
            return true;
        }

        private TrainCarContext CreateContext(TrainCarRenderSnapshot renderSnapshot)
        {
            // render 用 snapshot から processor 共通 context を作る
            // Build the shared processor context from the render snapshot
            return TrainCarContext.CreateAvailable(renderSnapshot.CurrentSpeed, renderSnapshot.MasconLevel);
        }

        private void DispatchProcessors(TrainCarContext context)
        {
            // 共通 context を各 processor へ配る
            // Dispatch the shared context to each processor
            for (var i = 0; i < _processors.Length; i++)
            {
                _processors[i].Update(context);
            }
        }

        private bool TryResolveLatestRenderSnapshot(uint tick, out TrainCarRenderSnapshot renderSnapshot)
        {
            // 出力値を先に初期化する
            // Initialize output values first
            renderSnapshot = default;

            // 車両 instance から対象 snapshot を解決する
            // Resolve the target snapshot from the car instance
            if (!_trainCache.TryGetCarSnapshot(_trainCarEntity.TrainCarInstanceId, out ClientTrainUnit unit, out TrainCarSnapshot snapshot, out var frontOffset, out var rearOffset))
            {
                return false;
            }

            // render 用 state に必要な値だけへ詰め替える
            // Convert the cache result into the render-facing snapshot
            var railPosition = unit.RailPosition;
            if (railPosition == null)
            {
                return false;
            }

            renderSnapshot = TrainCarRenderSnapshot.Create(tick, railPosition.DeepCopy(), frontOffset, rearOffset, snapshot.IsFacingForward, unit.CurrentSpeed, unit.MasconLevel);
            return true;
        }

    }
}
