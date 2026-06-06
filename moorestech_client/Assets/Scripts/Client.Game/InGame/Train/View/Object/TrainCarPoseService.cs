using System;
using Client.Game.InGame.Train.View;
using UnityEngine;

namespace Client.Game.InGame.Train.View.Object
{
    /// <summary>
    /// 列車車両の表示姿勢を同期するサービス
    /// Service that synchronizes train-car render pose.
    /// </summary>
    public class TrainCarPoseService
    {
        private readonly Transform _transform;
        private readonly TrainCarVisualPartBinding[] _visualParts;

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
            ModelForwardCenterOffset = ResolveModelForwardCenterOffset(transform, renderers);
            _visualParts = ResolveVisualParts();

            #region Internal

            TrainCarVisualPartBinding[] ResolveVisualParts()
            {
                // Prefab marker を順序付きで集める
                // Collect Prefab markers in authored order
                var parts = transform.GetComponentsInChildren<TrainCarVisualPart>(true);
                if (parts == null || parts.Length == 0)
                {
                    return Array.Empty<TrainCarVisualPartBinding>();
                }
                Array.Sort(parts, (left, right) => left.GetOrder().CompareTo(right.GetOrder()));

                // part ごとの target と renderer 中心補正を保持する
                // Store each part target and renderer-center correction
                var bindings = new TrainCarVisualPartBinding[parts.Length];
                for (var i = 0; i < parts.Length; i++)
                {
                    var part = parts[i];
                    var poseTarget = part.GetPoseTarget();
                    var partRenderers = poseTarget.GetComponentsInChildren<Renderer>(true);
                    var modelForwardCenterOffset = ResolveModelForwardCenterOffset(poseTarget, partRenderers);
                    bindings[i] = new TrainCarVisualPartBinding(poseTarget, part.GetLengthMeters(), modelForwardCenterOffset);
                }
                return bindings;
            }

            #endregion
        }

        public void RequestPose(Vector3 position, Quaternion rotation)
        {
            // tickで決まった姿勢を表示Transformへ即時反映する。
            // Apply the tick-resolved pose to the visible Transform immediately.
            _transform.SetPositionAndRotation(position, rotation);
        }

        public int GetVisualPartCount()
        {
            return _visualParts.Length;
        }

        public bool TryGetVisualPartLengthMeters(int index, out int lengthMeters)
        {
            // index の有効性を確認して authored 長を返す
            // Validate index and return the authored length
            lengthMeters = 0;
            if (index < 0 || _visualParts.Length <= index)
            {
                return false;
            }
            lengthMeters = _visualParts[index].LengthMeters;
            return lengthMeters > 0;
        }

        public bool TryGetVisualPartModelForwardCenterOffset(int index, out float modelForwardCenterOffset)
        {
            // index の有効性を確認して中心補正量を返す
            // Validate index and return the model-center correction
            modelForwardCenterOffset = 0f;
            if (index < 0 || _visualParts.Length <= index)
            {
                return false;
            }
            modelForwardCenterOffset = _visualParts[index].ModelForwardCenterOffset;
            return true;
        }

        public void RequestPartPose(int index, Vector3 position, Quaternion rotation)
        {
            // part target に補間済み world pose を反映する
            // Apply the interpolated world pose to the part target
            if (index < 0 || _visualParts.Length <= index)
            {
                return;
            }
            _visualParts[index].PoseTarget.SetPositionAndRotation(position, rotation);
        }

        private static float ResolveModelForwardCenterOffset(Transform targetTransform, Renderer[] renderers)
        {
            // renderer がない part は target origin を中心として扱う
            // Treat renderer-less parts as centered on their target origin
            if (renderers == null || renderers.Length == 0)
            {
                return 0f;
            }

            // Renderer群のワールド境界中心をローカル前後軸へ射影する。
            // Project the combined renderer bounds center onto the local forward axis.
            var combined = renderers[0].bounds;
            for (var i = 1; i < renderers.Length; i++) combined.Encapsulate(renderers[i].bounds);

            // モデルの見た目軸とレール進行方向の差分を補正して中心を測る。
            // Measure the center after correcting the model axis against rail direction.
            var localForwardAxis = Quaternion.Euler(0f, -TrainCarPoseCalculator.ModelYawOffsetDegrees, 0f) * Vector3.forward;
            var localCenter = targetTransform.InverseTransformPoint(combined.center);
            return Vector3.Dot(localCenter, localForwardAxis);
        }

        private readonly struct TrainCarVisualPartBinding
        {
            public readonly Transform PoseTarget;
            public readonly int LengthMeters;
            public readonly float ModelForwardCenterOffset;

            public TrainCarVisualPartBinding(Transform poseTarget, int lengthMeters, float modelForwardCenterOffset)
            {
                PoseTarget = poseTarget;
                LengthMeters = lengthMeters;
                ModelForwardCenterOffset = modelForwardCenterOffset;
            }
        }
    }
}
