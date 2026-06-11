using UnityEngine;

namespace Client.Game.InGame.Train.View.Object.Pose
{
    public static class TrainCarRailPositionVisualUtility
    {
        public static bool TryResolvePose(TrainCarRailPositionVisualState visualState, float modelForwardCenterOffset, out TrainCarPoseResult pose)
        {
            pose = default;
            if (!TrainCarPoseCalculator.TryResolveRenderPose(
                    visualState.RailPosition,
                    visualState.FrontOffset,
                    visualState.RearOffset,
                    visualState.IsFacingForward,
                    modelForwardCenterOffset,
                    out var position,
                    out var rotation))
            {
                return false;
            }

            pose = new TrainCarPoseResult(position, rotation);
            return true;
        }

        public static float ResolveModelForwardCenterOffset(Transform modelRoot)
        {
            var renderers = modelRoot.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                return 0f;
            }

            // renderer bounds中心をモデル前方軸のoffsetへ変換する
            // Convert renderer-bounds center to an offset along the corrected model-forward axis
            var bounds = renderers[0].bounds;
            for (var i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            // モデル軸補正後の前方軸へ中心点を射影し、X/Z軸の取り違えを避ける
            // Project the center onto the corrected local-forward axis instead of special-casing X/Z
            var localCenter = modelRoot.InverseTransformPoint(bounds.center);
            var localForwardAxis = Quaternion.Euler(0f, -TrainCarPoseCalculator.ModelYawOffsetDegrees, 0f) * Vector3.forward;
            return Vector3.Dot(localCenter, localForwardAxis.normalized);
        }
    }
}
