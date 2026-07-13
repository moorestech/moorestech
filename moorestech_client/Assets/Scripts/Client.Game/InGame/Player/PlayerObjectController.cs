using System;
using System.Linq;
using Client.Common;
using Client.Game.InGame.BlockSystem;
using Client.Network.API;
using StarterAssets;
using UnityEngine;
using VContainer;

namespace Client.Game.InGame.Player
{
    public interface IPlayerObjectController
    {
        public Vector3 Position { get; }
        public void SetPlayerPosition(Vector3 playerPos);
        public void SetActive(bool active);
        
        public void SetAnimationState(string state);
        public void SetControllable(bool enable);
        public void SetModelVisible(bool visible);
    }
    
    public class PlayerAnimationState
    {
        public const string IdleWalkRunBlend = "Idle Walk Run Blend";
        public const string JumpStart = "JumpStart";
        public const string JumpInAir = "JumpInAir";
        public const string JumpLand = "JumpLand";
        public const string Axe = "Axe";
    }
    
    public class PlayerObjectController : MonoBehaviour, IPlayerObjectController
    {
        public Vector3 Position => transform.position;
        public Vector2 Position2d => new(transform.position.x, transform.position.z);
        
        [SerializeField] private ThirdPersonController controller;
        [SerializeField] private Animator animator;
        private CharacterController characterController;
        private Transform rideFollowTarget;
        private bool _isModelVisible = true;
        private Vector3 rideFollowLocalPosition;
        private Quaternion rideFollowLocalRotation;
        private bool rideFollowStoredControllerEnabled;
        private bool rideFollowDisabledController;
        
        public void Initialize(InitialHandshakeResponse initialHandshakeResponse)
        {
            controller.Initialize();
            characterController = GetComponent<CharacterController>();
            SetPlayerPosition(initialHandshakeResponse.PlayerPos);
        }
        
        private void LateUpdate()
        {
            // 乗車中は補間済み車両poseを最後に反映する
            // Apply the interpolated train-car pose last while riding
            if (rideFollowTarget != null)
            {
                ApplyRideFollowPose();
                return;
            }

            // 通常時だけ落下復帰処理を行う
            // Run fall recovery only during normal player control
            if (transform.localPosition.y < -50)
            {
                var point = SlopeBlockPlaceSystem.GetGroundPoint(transform.position);
                if (point.HasValue)
                {
                    SetPlayerPosition(new Vector3(transform.localPosition.x, point.Value.y, transform.localPosition.z));
                }
                else
                {
                    var spawnPoint = FindObjectsByType<SpawnPointObject>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault();
                    if (spawnPoint == null)
                    {
                        SetPlayerPosition(new Vector3(0, 100, 0));
                        Debug.LogError("SpawnPointObject not found in the scene.");
                        return;
                    }
                    SetPlayerPosition(spawnPoint.transform.position);
                }
            }
        }
        
        /// <summary>
        ///     注意：アップデートのタイミングによってはThirdPersonController.csによる戻しが発生する可能性がある
        ///     セットしても位置が変わらなかった時はThirdPersonController.csをオフにして位置がセットできているか試してください
        /// </summary>
        /// <param name="playerPos"></param>
        public void SetPlayerPosition(Vector3 playerPos)
        {
            controller.Warp(playerPos);
        }
        
        public void SetActive(bool active)
        {
            gameObject.SetActive(active);
        }
        
        public void SetAnimationState(string state)
        {
            animator.Play(state);
        }
        public void SetControllable(bool enable)
        {
            controller.SetControllable(enable);
        }

        public void SetModelVisible(bool visible)
        {
            // 手持ちアイテムの生成破棄でRendererが入れ替わるためキャッシュせず毎回取得する
            // Grab items are created and destroyed under this hierarchy, so fetch renderers fresh each time
            _isModelVisible = visible;
            foreach (var modelRenderer in GetComponentsInChildren<Renderer>(true)) modelRenderer.enabled = visible;
        }

        // 手持ちアイテムの差し替え直後に呼ぶ。新Rendererは既定で表示のためFPSの非表示が漏れる
        // Called right after a grab item swap; new renderers default to visible and would leak through the FPS hide
        public void RefreshModelVisible()
        {
            SetModelVisible(_isModelVisible);
        }

        public void SetRideFollowTarget(Transform target, Vector3 localPosition, Quaternion localRotation)
        {
            // 乗車中はThirdPersonController側の重力・Move・足場追従を止める
            // Stop ThirdPersonController gravity, Move, and platform follow while riding
            DisableControllerForRideFollowIfNeeded();

            // 乗車追従のローカル基準を保存する
            // Store the local basis used for riding follow
            rideFollowTarget = target;
            rideFollowLocalPosition = localPosition;
            rideFollowLocalRotation = localRotation;
            
            SetControllable(false);
        }

        public void ClearRideFollowTarget()
        {
            // 乗車追従で止めたThirdPersonControllerの実行状態を戻す
            // Restore the ThirdPersonController execution state disabled for riding follow
            RestoreControllerAfterRideFollowIfNeeded();
            rideFollowTarget = null;
            SetControllable(true);
        }

        private void ApplyRideFollowPose()
        {
            // 車両の補間済みposeからプレイヤーのworld poseを作る
            // Build the player world pose from the interpolated train-car pose
            var worldPosition = rideFollowTarget.TransformPoint(rideFollowLocalPosition);
            var worldRotation = rideFollowTarget.rotation * rideFollowLocalRotation;

            // CharacterControllerの補正を避けて直接同期する
            // Bypass CharacterController correction while applying the pose directly
            characterController.enabled = false;
            transform.SetPositionAndRotation(worldPosition, worldRotation);
            characterController.enabled = true;
        }

        private void DisableControllerForRideFollowIfNeeded()
        {
            if (rideFollowTarget != null || rideFollowDisabledController)
            {
                return;
            }

            // 解除時に元の有効状態へ戻せるよう、乗車開始時だけ保存する
            // Store the original enabled state only when riding starts so dismount can restore it
            rideFollowStoredControllerEnabled = controller.enabled;
            controller.enabled = false;
            rideFollowDisabledController = true;
        }

        private void RestoreControllerAfterRideFollowIfNeeded()
        {
            if (!rideFollowDisabledController)
            {
                return;
            }

            // UI等で元々無効だった場合は、その無効状態を維持する
            // Preserve an originally disabled controller state such as UI control locks
            controller.enabled = rideFollowStoredControllerEnabled;
            rideFollowDisabledController = false;
        }
    }
}
