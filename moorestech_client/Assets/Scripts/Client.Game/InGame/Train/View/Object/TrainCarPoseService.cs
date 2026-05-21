using UnityEngine;

namespace Client.Game.InGame.Train.View.Object
{
    /// <summary>
    /// 列車車両の表示姿勢とRigidbody状態を同期するサービス
    /// Service that synchronizes train-car render pose and Rigidbody state.
    /// </summary>
    public class TrainCarPoseService
    {
        private const float ModelYawOffsetDegrees = -90f;

        private readonly Rigidbody _rigidbody;
        private readonly Transform _transform;

        /// <summary>
        /// モデル中心の前後オフセット
        /// Model forward center offset.
        /// </summary>
        public float ModelForwardCenterOffset { get; }

        public TrainCarPoseService(Rigidbody rigidbody, Transform transform, Renderer[] renderers)
        {
            _rigidbody = rigidbody;
            _transform = transform;

            // Rigidbodyは当たり判定用に残し、列車Transformはtick計算結果で直接動かす。
            // Keep the Rigidbody for collision while the train Transform is driven directly.
            ConfigureRigidbody();

            // モデル境界から姿勢計算用の中心補正量を求める。
            // Resolve the model center offset used by pose calculation.
            ModelForwardCenterOffset = ResolveModelForwardCenterOffset();

            #region Internal

            void ConfigureRigidbody()
            {
                // 物理エンジンが列車Transformを補間・外挿しないようにする。
                // Prevent the physics engine from interpolating or extrapolating the train Transform.
                _rigidbody.isKinematic = true;
                _rigidbody.useGravity = false;
                _rigidbody.interpolation = RigidbodyInterpolation.None;
                _rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            }

            float ResolveModelForwardCenterOffset()
            {
                // Renderer群のワールド境界中心をローカル前後軸へ射影する。
                // Project the combined renderer bounds center onto the local forward axis.
                var combined = renderers[0].bounds;
                for (var i = 1; i < renderers.Length; i++) combined.Encapsulate(renderers[i].bounds);

                // モデルの見た目軸とレール進行方向の差分を補正して中心を測る。
                // Measure the center after correcting the model axis against rail direction.
                var localForwardAxis = Quaternion.Euler(0f, -ModelYawOffsetDegrees, 0f) * Vector3.forward;
                var localCenter = transform.InverseTransformPoint(combined.center);
                return Vector3.Dot(localCenter, localForwardAxis);
            }

            #endregion
        }

        public void RequestPose(Vector3 position, Quaternion rotation)
        {
            // tickで決まった姿勢を表示Transformへ即時反映する。
            // Apply the tick-resolved pose to the visible Transform immediately.
            _transform.SetPositionAndRotation(position, rotation);

            // RigidbodyはTransformを主導せず、同じ姿勢へ同期するだけにする。
            // Keep the Rigidbody synchronized without letting it drive the Transform.
            _rigidbody.position = position;
            _rigidbody.rotation = rotation;
        }
    }
}
