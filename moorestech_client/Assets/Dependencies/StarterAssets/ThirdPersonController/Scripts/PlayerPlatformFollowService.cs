using UnityEngine;

namespace StarterAssets
{
    /// <summary>
    /// 足元のkinematic Rigidbody（列車などの可動足場）の位置・回転をプレイヤーへ追従させる純粋C#サービス
    /// Pure C# service that makes the player follow the position/rotation of the kinematic Rigidbody beneath the feet (e.g. trains)
    /// </summary>
    public class PlayerPlatformFollowService
    {
        private static readonly Collider[] _platformOverlapBuffer = new Collider[8];

        private readonly Transform _transform;
        private readonly CharacterController _controller;

        private Rigidbody _trackedPlatformRigidbody;
        private Vector3 _trackedPlatformPosition;
        private Quaternion _trackedPlatformRotation;

        public PlayerPlatformFollowService(Transform transform, CharacterController controller)
        {
            _transform = transform;
            _controller = controller;
        }

        /// <summary>
        /// 足元のkinematic Rigidbodyの前フレームからの移動量を取得し、衝突解消で吸収されないように直接反映する
        /// Resolve the per-frame delta of the kinematic Rigidbody beneath the player and apply it bypassing collision resolve
        /// </summary>
        public void ApplyPlatformFollow(bool grounded, float groundedOffset, float groundedRadius, LayerMask groundLayers)
        {
            var delta = ResolveMovingPlatformDelta();
            ApplyPlatformDelta(delta);

            #region Internal

            Vector3 ResolveMovingPlatformDelta()
            {
                // 接地していない、またはForceRide等で既にkinematic Rigidbody配下に親付けされている場合は適用しない
                // Skip when airborne or already parented under a kinematic Rigidbody (e.g. ForceRide)
                if (!grounded || IsParentedToKinematicRigidbody())
                {
                    _trackedPlatformRigidbody = null;
                    return Vector3.zero;
                }

                var platform = FindGroundKinematicRigidbody();

                var resolved = Vector3.zero;
                if (platform != null && platform == _trackedPlatformRigidbody)
                {
                    // 前フレームの平台ローカルでのプレイヤー位置を、現フレームの平台ワールドへ再投影する
                    // Reproject the player's prior platform-local position into the current platform world (handles curve rotation)
                    var localOffset = Quaternion.Inverse(_trackedPlatformRotation) * (_transform.position - _trackedPlatformPosition);
                    var expectedNewWorld = platform.transform.position + platform.transform.rotation * localOffset;
                    resolved = expectedNewWorld - _transform.position;
                }

                _trackedPlatformRigidbody = platform;
                if (platform != null)
                {
                    _trackedPlatformPosition = platform.transform.position;
                    _trackedPlatformRotation = platform.transform.rotation;
                }
                return resolved;
            }

            void ApplyPlatformDelta(Vector3 platformDelta)
            {
                // CharacterControllerの衝突解消が水平deltaを吸収するため、enable切り替えでバイパスして直接transformへ書き込む
                // Bypass CharacterController collision resolve by toggling enabled around a direct transform write
                if (platformDelta.sqrMagnitude <= 0f)
                {
                    return;
                }
                _controller.enabled = false;
                _transform.position += platformDelta;
                _controller.enabled = true;
            }

            bool IsParentedToKinematicRigidbody()
            {
                // シーン階層上の単なる親付け（PlayerSystem等）は通し、kinematic Rigidbody直下のときだけ弾く
                // Allow normal scene-hierarchy parenting; only block when nested under a kinematic Rigidbody
                if (_transform.parent == null)
                {
                    return false;
                }
                var parentRigidbody = _transform.parent.GetComponentInParent<Rigidbody>();
                return parentRigidbody != null && parentRigidbody.isKinematic;
            }

            Rigidbody FindGroundKinematicRigidbody()
            {
                // 接地判定と同じ球・同じレイヤで足元のColliderを探索する
                // Probe the same grounded sphere and layers used by GroundedCheck
                var spherePosition = new Vector3(_transform.position.x, _transform.position.y - groundedOffset, _transform.position.z);
                var count = Physics.OverlapSphereNonAlloc(spherePosition, groundedRadius, _platformOverlapBuffer, groundLayers, QueryTriggerInteraction.Ignore);
                for (var i = 0; i < count; i++)
                {
                    var rb = _platformOverlapBuffer[i].attachedRigidbody;
                    if (rb != null && rb.isKinematic)
                    {
                        return rb;
                    }
                }
                return null;
            }

            #endregion
        }

        /// <summary>
        /// ワープ等で追従元との連続性が失われる際にトラッキング状態をリセットする
        /// Reset tracking when continuity with the tracked platform is lost (e.g. warp)
        /// </summary>
        public void ResetTracking()
        {
            _trackedPlatformRigidbody = null;
        }
    }
}
