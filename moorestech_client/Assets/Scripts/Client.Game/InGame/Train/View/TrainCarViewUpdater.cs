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
        private TrainCarEntityObject _trainCarEntity;
        private ITrainCarObjectProcessor[] _processors;
        private bool _isReady;

        public void SetDependencies(TrainCarEntityObject trainCarEntity, TrainUnitClientCache trainCache)
        {
            // 依存参照と表示 processor 一覧を初期化時に保持する
            // Store dependency references and view processors during initialization
            _trainCarEntity = trainCarEntity;
            _trainCache = trainCache;
            _processors = ResolveProcessors();
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

            // cache から姿勢と processor 向け context をまとめて解決する
            // Resolve pose and processor context from the client cache
            if (!TryResolveUnitState(out var position, out var rotation, out var context))
            {
                DispatchProcessors(TrainCarContext.CreateUnavailable());
                return;
            }

            // レール由来の姿勢を GameObject に反映する
            // Apply the rail-derived pose to the GameObject
            _trainCarEntity.SetDirectPose(position, rotation);
            DispatchProcessors(context);
        }

        #region Internal

        private bool TryResolveUnitState(out Vector3 position, out Quaternion rotation, out TrainCarContext context)
        {
            // 出力値を先に初期化する
            // Initialize output values first
            position = default;
            rotation = Quaternion.identity;
            context = TrainCarContext.CreateUnavailable();

            // 車両 index から現在 snapshot を解決する
            // Resolve the current snapshot from the car index
            if (!TryResolveCarSnapshot(out var unit, out var snapshot, out var frontOffset, out var rearOffset))
            {
                return false;
            }

            // レール上の前後位置から車両姿勢を計算する
            // Compute rail pose from front and rear wheel positions
            var railPosition = unit.RailPosition;
            if (railPosition == null)
            {
                return false;
            }
            if (!TryResolveCarPose(railPosition, frontOffset, rearOffset, out position, out var forward))
            {
                return false;
            }

            // モデル補正込みで最終姿勢を構成する
            // Finalize rotation and forward offset with model correction
            rotation = BuildRotation(forward, snapshot.IsFacingForward);
            var localForwardAxis = Quaternion.Euler(0f, -ModelYawOffsetDegrees, 0f) * Vector3.forward;
            var modelForward = rotation * localForwardAxis;
            position -= modelForward * _trainCarEntity.ModelForwardCenterOffset;
            context = TrainCarContext.CreateAvailable(unit.CurrentSpeed, unit.MasconLevel);
            return true;
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

        private ITrainCarObjectProcessor[] ResolveProcessors()
        {
            // 子 MonoBehaviour から processor 実装だけを集める
            // Collect only processor implementations from child MonoBehaviours
            var behaviours = GetComponentsInChildren<MonoBehaviour>(true);
            var processorCount = 0;
            for (var i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is ITrainCarObjectProcessor)
                {
                    processorCount++;
                }
            }

            // 見つけた順序を保って processor 配列を構成する
            // Preserve discovery order when building processor array
            var processors = new ITrainCarObjectProcessor[processorCount];
            var processorIndex = 0;
            for (var i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is not ITrainCarObjectProcessor processor)
                {
                    continue;
                }

                processors[processorIndex] = processor;
                processorIndex++;
            }

            return processors;
        }

        private bool TryResolveCarSnapshot(out ClientTrainUnit unit, out TrainCarSnapshot snapshot, out int frontOffset, out int rearOffset)
        {
            // 出力値を先に初期化する
            // Initialize output values first
            unit = null;
            snapshot = default;
            frontOffset = 0;
            rearOffset = 0;

            // 車両 instance から対象 snapshot を解決する
            // Resolve the target snapshot from the car instance
            return _trainCache.TryGetCarSnapshot(_trainCarEntity.TrainCarInstanceId, out unit, out snapshot, out frontOffset, out rearOffset);
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

        #endregion
    }
}
