using UnityEngine;

namespace Client.Game.InGame.Train.View.Object
{
    /// <summary>
    /// 列車車両のRigidbody姿勢制御を担う純粋C#サービス
    /// Pure C# service responsible for train car rigidbody pose control
    /// </summary>
    public class TrainCarPoseService
    {
        private const float ModelYawOffsetDegrees = -90f;

        private readonly Rigidbody _rigidbody;
        private readonly Transform _transform;
        private Vector3 _requestedPosition;
        private Quaternion _requestedRotation = Quaternion.identity;
        private bool _hasRequestedPose;
        private bool _hasAppliedInitialPose;

        /// <summary>
        /// モデル中心の前後オフセット
        /// Model forward center offset
        /// </summary>
        public float ModelForwardCenterOffset { get; }

        public TrainCarPoseService(Rigidbody rigidbody, Transform transform, Renderer[] renderers)
        {
            _rigidbody = rigidbody;
            _transform = transform;
            // Rigidbodyをkinematic用に設定する
            // Configure the Rigidbody for kinematic pose driving
            ConfigureRigidbody();
            // モデル中心の前後オフセットをキャッシュする
            // Cache the model forward center offset
            ModelForwardCenterOffset = ResolveModelForwardCenterOffset();

            #region Internal

            void ConfigureRigidbody()
            {
                // 列車Colliderの移動を物理エンジンへ渡すためRigidbodyをkinematicに設定する
                // Set the Rigidbody kinematic so train collider movement is handled by physics
                _rigidbody.isKinematic = true;
                _rigidbody.useGravity = false;
                _rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
                _rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            }

            float ResolveModelForwardCenterOffset()
            {
                // レンダラの境界中心から前後オフセットを算出する
                // Compute forward offset from renderer bounds center
                var combined = renderers[0].bounds;
                for (var i = 1; i < renderers.Length; i++) combined.Encapsulate(renderers[i].bounds);
                var localForwardAxis = Quaternion.Euler(0f, -ModelYawOffsetDegrees, 0f) * Vector3.forward;
                var localCenter = transform.InverseTransformPoint(combined.center);
                return Vector3.Dot(localCenter, localForwardAxis);
            }

            #endregion
        }

        /// <summary>
        /// 物理更新で反映する列車姿勢を要求する
        /// Request the train pose to apply during physics updates
        /// </summary>
        public void RequestPose(Vector3 position, Quaternion rotation)
        {
            _requestedPosition = position;
            _requestedRotation = rotation;
            _hasRequestedPose = true;

            // 初回だけ物理補間せずに正しい位置へ配置する
            // Snap only the first pose so the train starts at the correct location
            if (_hasAppliedInitialPose)
            {
                return;
            }

            _rigidbody.position = position;
            _rigidbody.rotation = rotation;
            _transform.SetPositionAndRotation(position, rotation);
            _hasAppliedInitialPose = true;
        }

        /// <summary>
        /// FixedUpdateで姿勢要求を物理エンジンへ反映する
        /// Apply the pose request to the physics engine on FixedUpdate
        /// </summary>
        public void ApplyToPhysics()
        {
            // 姿勢要求が届くまで物理更新は行わない
            // Skip physics movement until a pose request is available
            if (!_hasRequestedPose)
            {
                return;
            }

            // kinematic Rigidbody経由でCollider移動を物理エンジンへ反映する
            // Apply collider movement through the kinematic Rigidbody
            _rigidbody.MovePosition(_requestedPosition);
            _rigidbody.MoveRotation(_requestedRotation);
        }
    }
}
