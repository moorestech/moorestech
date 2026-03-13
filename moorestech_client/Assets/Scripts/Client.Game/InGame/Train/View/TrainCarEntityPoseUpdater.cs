using System;
using Client.Game.InGame.Train.Unit;
using Client.Game.InGame.Train.View.Object;
using Game.Train.RailPositions;
using Game.Train.Unit;
using UnityEngine;

namespace Client.Game.InGame.Train.View
{
    public sealed class TrainCarEntityPoseUpdater : MonoBehaviour
    {
        // 列車モデルの前方軸補正をレール進行方向に合わせる
        // Model forward axis correction to match rail direction
        private const double AnimationSpeed = 0.0025;
        private const float ModelYawOffsetDegrees = -90f;
        private TrainUnitClientCache _trainCache;
        private TrainCarEntityObject _trainCarEntity;
        private Animator[] _animators;
        private float[] _animatorBaseSpeeds;
        private bool _isReady;

        public void SetDependencies(TrainCarEntityObject trainCarEntity, TrainUnitClientCache trainCache)
        {
            // 依存参照と Animator 一覧を初期化時に保持する
            // Store dependency references and Animator list during initialization
            _trainCarEntity = trainCarEntity;
            _trainCache = trainCache;
            _animators = GetComponentsInChildren<Animator>(true);
            _animatorBaseSpeeds = new float[_animators.Length];
            _isReady = true;
        }

        private void Update()
        {
            // 初期化完了後のみ列車表示を更新する
            // Update the train view only after initialization completes
            if (!_isReady) return;

            // キャッシュから姿勢と速度をまとめて解決する
            // Resolve pose and speed together from the client cache
            if (!TryResolveUnitState(out var position, out var rotation, out var currentSpeed))
            {
                ApplyAnimationSpeed(0.0);
                return;
            }

            // レール計算結果を GameObject に反映する
            // Apply the rail-derived pose to the GameObject
            _trainCarEntity.SetDirectPose(position, rotation);

            // 列車速度に同期した再生速度を Animator に反映する
            // Apply animation playback speed synchronized to train speed
            ApplyAnimationSpeed(currentSpeed);
        }

        #region Internal

        private bool TryResolveUnitState(out Vector3 position, out Quaternion rotation, out double currentSpeed)
        {
            // 出力値を先に初期化しておく
            // Initialize output values first
            position = default;
            rotation = Quaternion.identity;
            currentSpeed = 0.0;

            // 車両インデックスから現在 snapshot を引く
            // Resolve the current snapshot from the car index
            if (!TryResolveCarSnapshot(out var unit, out var snapshot, out var frontOffset, out var rearOffset)) return false;
            currentSpeed = unit.CurrentSpeed;

            // レール上の姿勢を前後輪位置から計算する
            // Compute rail pose from front and rear wheel positions
            var railPosition = unit.RailPosition;
            if (railPosition == null) return false;
            if (!TryResolveCarPose(railPosition, frontOffset, rearOffset, out position, out var forward)) return false;

            // モデル補正込みの回転と前後オフセットを確定する
            // Finalize rotation and forward offset with model correction
            rotation = BuildRotation(forward, snapshot.IsFacingForward);
            var localForwardAxis = Quaternion.Euler(0f, -ModelYawOffsetDegrees, 0f) * Vector3.forward;
            var modelForward = rotation * localForwardAxis;
            position -= modelForward * _trainCarEntity.ModelForwardCenterOffset;
            return true;
        }

        private void ApplyAnimationSpeed(double currentSpeed)
        {
            // Animator が無い列車モデルでは何もしない
            // Do nothing for train models without an Animator
            if (_animators == null || _animators.Length == 0)
            {
                return;
            }

            // 物理側の移動量に比例した再生速度を作る
            // Build playback speed proportional to motion distance
            var playbackSpeed = (float)(Math.Abs(currentSpeed) * AnimationSpeed);
            for (var i = 0; i < _animators.Length; i++)
            {
                var animator = _animators[i];
                if (animator == null)
                {
                    continue;
                }

                // Controller 側の既定速度を保ちつつ Animator.speed で上乗せする
                // Preserve controller-authored base speed and scale via Animator.speed
                var baseSpeed = ResolveAnimatorBaseSpeed(i, animator);
                animator.speed = playbackSpeed / baseSpeed;
            }
        }

        private float ResolveAnimatorBaseSpeed(int index, Animator animator)
        {
            // 一度解決した既定速度は再利用する
            // Reuse the resolved authored speed once captured
            if (_animatorBaseSpeeds[index] > 0f)
            {
                return _animatorBaseSpeeds[index];
            }

            // Animator.speed を差し引いて state 側の素の速度を得る
            // Strip Animator.speed to recover the controller-authored state speed
            var currentAnimatorSpeed = Mathf.Abs(animator.speed);
            if (currentAnimatorSpeed <= Mathf.Epsilon)
            {
                currentAnimatorSpeed = 1f;
            }

            var stateSpeed = animator.GetCurrentAnimatorStateInfo(0).speed;
            var resolvedBaseSpeed = stateSpeed > 0f ? stateSpeed / currentAnimatorSpeed : 1f;
            _animatorBaseSpeeds[index] = resolvedBaseSpeed;
            return resolvedBaseSpeed;
        }

        private bool TryResolveCarSnapshot(out ClientTrainUnit unit, out TrainCarSnapshot snapshot, out int frontOffset, out int rearOffset)
        {
            // 出力値を先に初期化しておく
            // Initialize output values first
            unit = null;
            snapshot = default;
            frontOffset = 0;
            rearOffset = 0;

            // 車両インデックスから対象 snapshot を取得する
            // Resolve the target snapshot from the car index
            return _trainCache.TryGetCarSnapshot(_trainCarEntity.TrainCarInstanceId, out unit, out snapshot, out frontOffset, out rearOffset);
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
            // 正規化した進行方向から回転を作る
            // Build rotation from the normalized forward vector
            var safeForward = forward.sqrMagnitude > 1e-6f ? forward.normalized : Vector3.forward;
            var rotation = Quaternion.LookRotation(safeForward, Vector3.up);

            // モデル前方軸の補正と編成向き反転を適用する
            // Apply model axis correction and formation facing inversion
            rotation = rotation * Quaternion.Euler(0f, ModelYawOffsetDegrees, 0f);
            if (!isFacingForward) rotation = rotation * Quaternion.Euler(0f, 180f, 0f);
            return rotation;
        }

        #endregion
    }
}
