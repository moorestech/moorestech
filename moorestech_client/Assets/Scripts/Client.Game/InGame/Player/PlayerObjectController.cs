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
        private Vector3 rideFollowLocalPosition;
        private Quaternion rideFollowLocalRotation;
        
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

        public void SetRideFollowTarget(Transform target, Vector3 localPosition, Quaternion localRotation)
        {
            // 乗車追従のローカル基準を保存する
            // Store the local basis used for riding follow
            rideFollowTarget = target;
            rideFollowLocalPosition = localPosition;
            rideFollowLocalRotation = localRotation;
        }

        public void ClearRideFollowTarget()
        {
            rideFollowTarget = null;
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
    }
}
