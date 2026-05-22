using UnityEngine;

namespace Client.Game.InGame.Train.View.Object
{
    /// <summary>
    /// 列車車両の表示姿勢を同期するサービス
    /// Service that synchronizes train-car render pose.
    /// </summary>
    public class TrainCarPoseService
    {
        private const float ModelYawOffsetDegrees = -90f;

        private readonly Transform _transform;

        /// <summary>
        /// モデル中心の前後オフセット
        /// Model forward center offset.
        /// </summary>
        public float ModelForwardCenterOffset { get; }

        public TrainCarPoseService(Transform transform, Renderer[] renderers)
        {
            _transform = transform;

            // モデル境界から姿勢計算用の中心補正量を求める。
            // Resolve the model center offset used by pose calculation.
            ModelForwardCenterOffset = ResolveModelForwardCenterOffset();

            #region Internal

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
        }
    }
}
