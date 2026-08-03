using StarterAssets;
using UnityEngine;

namespace Client.Game.InGame.Player
{
    // 乗車中のプレイヤー追従と、そのために必要なThirdPersonControllerの停止・復元を担う
    // Owns the player follow while riding and the ThirdPersonController disable/restore it requires
    public class PlayerRideFollow
    {
        private readonly Transform _playerTransform;
        private readonly CharacterController _characterController;
        private readonly ThirdPersonController _thirdPersonController;

        private Transform _followTarget;
        private Vector3 _localPosition;
        private Quaternion _localRotation;
        private bool _storedControllerEnabled;
        private bool _disabledController;

        public PlayerRideFollow(Transform playerTransform, CharacterController characterController, ThirdPersonController thirdPersonController)
        {
            _playerTransform = playerTransform;
            _characterController = characterController;
            _thirdPersonController = thirdPersonController;
        }

        public bool IsFollowing()
        {
            return _followTarget != null;
        }

        public void SetTarget(Transform target, Vector3 localPosition, Quaternion localRotation)
        {
            // 乗車中はThirdPersonController側の重力・Move・足場追従を止める
            // Stop ThirdPersonController gravity, Move, and platform follow while riding
            DisableControllerIfNeeded();

            // 乗車追従のローカル基準を保存する
            // Store the local basis used for riding follow
            _followTarget = target;
            _localPosition = localPosition;
            _localRotation = localRotation;
        }

        public void ClearTarget()
        {
            // 乗車追従で止めたThirdPersonControllerの実行状態を戻す
            // Restore the ThirdPersonController execution state disabled for riding follow
            RestoreControllerIfNeeded();
            _followTarget = null;
        }

        public void ApplyPose()
        {
            // 車両の補間済みposeからプレイヤーのworld poseを作る
            // Build the player world pose from the interpolated train-car pose
            var worldPosition = _followTarget.TransformPoint(_localPosition);
            var worldRotation = _followTarget.rotation * _localRotation;

            // CharacterControllerの補正を避けて直接同期する
            // Bypass CharacterController correction while applying the pose directly
            _characterController.enabled = false;
            _playerTransform.SetPositionAndRotation(worldPosition, worldRotation);
            _characterController.enabled = true;
        }

        private void DisableControllerIfNeeded()
        {
            if (_followTarget != null || _disabledController)
            {
                return;
            }

            // 解除時に元の有効状態へ戻せるよう、乗車開始時だけ保存する
            // Store the original enabled state only when riding starts so dismount can restore it
            _storedControllerEnabled = _thirdPersonController.enabled;
            _thirdPersonController.enabled = false;
            _disabledController = true;
        }

        private void RestoreControllerIfNeeded()
        {
            if (!_disabledController)
            {
                return;
            }

            // UI等で元々無効だった場合は、その無効状態を維持する
            // Preserve an originally disabled controller state such as UI control locks
            _thirdPersonController.enabled = _storedControllerEnabled;
            _disabledController = false;
        }
    }
}
