using Client.Game.InGame.Entity.Object;
using Game.Train.RailGraph;
using Game.Train.Train;
using Game.Train.Utility;
using UnityEngine;

namespace Client.Game.InGame.Train
{
    public sealed class TrainCarEntityPoseUpdater : MonoBehaviour
    {
        // 車両モデルの前方向補正量をレール進行方向に合わせる
        // Model forward axis correction to match rail direction
        private const float ModelYawOffsetDegrees = 90f;
        private TrainUnitClientCache _trainCache;
        private TrainCarEntityObject _trainCarEntity;
        private bool _isReady;

        public void SetDependencies(TrainCarEntityObject trainCarEntity, TrainUnitClientCache trainCache)
        {
            // 姿勢更新に必要な参照を保持する
            // Store references required for pose updates
            _trainCarEntity = trainCarEntity;
            _trainCache = trainCache;
            _isReady = true;
        }

        private void Update()
        {
            // 初期化が完了している場合だけ姿勢を更新する
            // Update pose only after initialization is complete
            if (!_isReady) return;

            // 列車キャッシュから姿勢を計算する
            // Calculate pose from train cache
            if (!TryResolveUnitPose(out var position, out var rotation)) return;

            // 計算結果をGameObjectに反映する
            // Apply the computed pose to the GameObject
            _trainCarEntity.SetDirectPose(position, rotation);
        }

        #region Internal

        private bool TryResolveUnitPose(out Vector3 position, out Quaternion rotation)
        {
            // 出力を初期化する
            // Initialize output values
            position = default;
            rotation = Quaternion.identity;

            // 対象車両のスナップショットを探す
            // Find the snapshot for this car
            if (!TryResolveCarSnapshot(out var unit, out var snapshot, out var frontOffset, out var rearOffset)) return false;

            // レール上の姿勢を計算する
            // Compute pose on the rail
            var railPosition = unit.RailPosition;
            if (railPosition == null) return false;
            if (!TryResolveCarPose(railPosition, frontOffset, rearOffset, out position, out var forward)) return false;

            // モデル補正を加えて回転と位置を確定する
            // Finalize rotation and position with model correction
            rotation = BuildRotation(forward, snapshot.IsFacingForward);
            var modelForward = rotation * Vector3.forward;
            position -= modelForward * _trainCarEntity.ModelForwardCenterOffset;
            return true;
        }

        private bool TryResolveCarSnapshot(out ClientTrainUnit unit, out TrainCarSnapshot snapshot, out int frontOffset, out int rearOffset)
        {
            // 出力を初期化する
            // Initialize output values
            unit = null;
            snapshot = default;
            frontOffset = 0;
            rearOffset = 0;

            // 車両索引からスナップショットを取得する
            // Resolve the snapshot from the car index
            return _trainCache.TryGetCarSnapshot(_trainCarEntity.TrainCarId, out unit, out snapshot, out frontOffset, out rearOffset);
        }


        private bool TryResolveCarPose(RailPosition railPosition, int frontOffset, int rearOffset, out Vector3 position, out Vector3 forward)
        {
            // 前輪と後輪の位置から車両姿勢を算出する
            // Compute the car pose from front and rear wheel positions
            position = default;
            forward = Vector3.forward;
            if (!TrainCarPoseCalculator.TryGetPose(railPosition, frontOffset, out var frontPosition, out var frontForward)) return false;
            if (!TrainCarPoseCalculator.TryGetPose(railPosition, rearOffset, out var rearPosition, out _)) return false;
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
            // モデル前方向の差を補正する
            // Correct the model forward axis offset
            rotation = rotation * Quaternion.Euler(0f, ModelYawOffsetDegrees, 0f);
            if (!isFacingForward) rotation = rotation * Quaternion.Euler(0f, 180f, 0f);
            return rotation;
        }

        #endregion
    }
}
