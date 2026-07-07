using UnityEngine;

namespace Client.Game.InGame.Control
{
    /// <summary>
    ///     カメラTweenの目標回転・距離・時間
    ///     Target rotation, distance, and duration for a camera tween
    /// </summary>
    public class TweenCameraInfo
    {
        public const float DefaultTweenDuration = 0.25f;

        public readonly Vector3 Rotation;
        public readonly float Distance;
        public readonly float TweenDuration;

        public TweenCameraInfo(Vector3 rotation, float distance, float tweenDuration = DefaultTweenDuration)
        {
            Rotation = rotation;
            Distance = distance;
            TweenDuration = tweenDuration;
        }
    }
}
