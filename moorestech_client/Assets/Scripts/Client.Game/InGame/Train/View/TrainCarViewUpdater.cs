using Client.Game.InGame.Train.Unit;
using Client.Game.InGame.Train.View.Object;
using Game.Train.RailPositions;
using Game.Train.Unit;
using UnityEngine;

namespace Client.Game.InGame.Train.View
{
    public sealed class TrainCarViewUpdater : MonoBehaviour
    {
        // 列車モデルの前方軸補正をレール進行方向に合わせる
        // Model forward axis correction to match rail direction
        private const float ModelYawOffsetDegrees = -90f;

        private TrainUnitClientCache _trainCache;
        private TrainUnitTickState _tickState;
        private TrainUnitClientSimulator _clientSimulator;
        private TrainCarEntityObject _trainCarEntity;
        private ITrainCarObjectProcessor[] _processors;
        private TrainCarRenderSnapshot _currentRenderSnapshot;
        private TrainCarRenderSnapshot _previousRenderSnapshot;
        private uint _lastSnapshotTick;
        private bool _hasCurrentRenderSnapshot;
        private bool _hasPreviousRenderSnapshot;
        private bool _isReady;

        public void Initialize(TrainCarEntityObject trainCarEntity, TrainUnitClientCache trainCache, TrainUnitTickState tickState, TrainUnitClientSimulator clientSimulator)
        {
            // 依存参照と表示 processor 一覧を初期化時に保持する
            // Store dependency references and view processors during initialization
            _trainCarEntity = trainCarEntity;
            _trainCache = trainCache;
            _tickState = tickState;
            _clientSimulator = clientSimulator;
            _currentRenderSnapshot = default;
            _previousRenderSnapshot = default;
            _lastSnapshotTick = 0;
            _hasCurrentRenderSnapshot = false;
            _hasPreviousRenderSnapshot = false;
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
            DispatchProcessors(CreateContext(contextSnapshot));
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

            var rate = (float)_clientSimulator.GetRenderInterpolationRate();
            position = Vector3.LerpUnclamped(previousPosition, currentPosition, rate);
            rotation = Quaternion.SlerpUnclamped(previousRotation, currentRotation, rate);
            return true;
        }

        private bool TryResolveRenderPose(TrainCarRenderSnapshot renderSnapshot, out Vector3 position, out Quaternion rotation)
        {
            // 出力値を先に初期化する
            // Initialize output values first
            position = default;
            rotation = Quaternion.identity;

            // レール上の前後位置から車両姿勢を計算する
            // Compute rail pose from front and rear wheel positions
            if (!TryResolveCarPose(renderSnapshot.RailPosition, renderSnapshot.FrontOffset, renderSnapshot.RearOffset, out position, out var forward))
            {
                return false;
            }

            // モデル補正込みで最終姿勢を構成する
            // Finalize rotation and forward offset with model correction
            rotation = BuildRotation(forward, renderSnapshot.IsFacingForward);
            var localForwardAxis = Quaternion.Euler(0f, -ModelYawOffsetDegrees, 0f) * Vector3.forward;
            var modelForward = rotation * localForwardAxis;
            position -= modelForward * _trainCarEntity.ModelForwardCenterOffset;
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

        private bool TryResolveCarPose(RailPosition railPosition, int frontOffset, int rearOffset, out Vector3 position, out Vector3 forward)
        {
            // 前後輪位置から車両中心姿勢を計算する
            // Compute the car pose from front and rear wheel positions
            position = default;
            forward = Vector3.forward;
            if (!TrainCarPoseCalculator.TryGetPose(railPosition, frontOffset, out var frontPosition, out var frontForward))
            {
                return false;
            }
            if (!TrainCarPoseCalculator.TryGetPose(railPosition, rearOffset, out var rearPosition, out _))
            {
                return false;
            }

            position = (frontPosition + rearPosition) * 0.5f;
            var delta = frontPosition - rearPosition;
            forward = delta.sqrMagnitude > 1e-6f ? delta.normalized : (frontForward.sqrMagnitude > 1e-6f ? frontForward.normalized : Vector3.forward);
            return true;
        }

        private Quaternion BuildRotation(Vector3 forward, bool isFacingForward)
        {
            // 正規化した前方ベクトルから回転を構成する
            // Build rotation from the normalized forward vector
            var safeForward = forward.sqrMagnitude > 1e-6f ? forward.normalized : Vector3.forward;
            var rotation = Quaternion.LookRotation(safeForward, Vector3.up);

            // モデル軸補正と編成向き反転を適用する
            // Apply model axis correction and formation facing inversion
            rotation = rotation * Quaternion.Euler(0f, ModelYawOffsetDegrees, 0f);
            if (!isFacingForward)
            {
                rotation = rotation * Quaternion.Euler(0f, 180f, 0f);
            }

            return rotation;
        }
    }
}
