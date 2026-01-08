using Client.Game.InGame.Entity.Object;
using Core.Master;
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
        private TrainCarPoseCalculator _poseCalculator;
        private TrainCarEntityObject _trainCarEntity;
        private bool _isReady;

        public void SetDependencies(TrainCarEntityObject trainCarEntity, TrainUnitClientCache trainCache, TrainCarPoseCalculator poseCalculator)
        {
            // 姿勢更新に必要な参照を保持する
            // Store references required for pose updates
            _trainCarEntity = trainCarEntity;
            _trainCache = trainCache;
            _poseCalculator = poseCalculator;
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

            // 列車キャッシュから対象車両を探索する
            // Search train cache for the target car
            foreach (var candidate in _trainCache.Units.Values)
            {
                var carSnapshots = candidate.Cars;
                if (carSnapshots.Count == 0) continue;

                // 先頭からの距離を積み上げる
                // Accumulate distance from head
                var offsetFromHead = 0;
                for (var i = 0; i < carSnapshots.Count; i++)
                {
                    // 対象車両かを判定しオフセットを算出する
                    // Determine target car and compute offsets
                    var carSnapshot = carSnapshots[i];
                    var isTargetCar = carSnapshot.CarId == _trainCarEntity.TrainCarId;
                    var carLength = ResolveCarLength(carSnapshot, isTargetCar ? _trainCarEntity : null);
                    if (carLength <= 0) continue;
                    var frontOffsetCandidate = offsetFromHead;
                    var rearOffsetCandidate = offsetFromHead + carLength;
                    if (!isTargetCar)
                    {
                        offsetFromHead += carLength;
                        continue;
                    }

                    // 対象車両の結果を確定する
                    // Finalize the result for the target car
                    unit = candidate;
                    snapshot = carSnapshot;
                    frontOffset = frontOffsetCandidate;
                    rearOffset = rearOffsetCandidate;
                    return true;
                }
            }

            return false;
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
            // モデル前方向の差を補正する
            // Correct the model forward axis offset
            rotation = rotation * Quaternion.Euler(0f, ModelYawOffsetDegrees, 0f);
            if (!isFacingForward) rotation = rotation * Quaternion.Euler(0f, 180f, 0f);
            return rotation;
        }

        #endregion
    }
}
