using UnityEngine;

namespace Client.Game.InGame.Control.ViewMode
{
    public class ThirdPersonCameraDistance
    {
        public const float MinimumDistance = 0.6f;
        public const float MaximumDistance = 10f;

        private float _distance;
        private bool _isTransitioning;

        public ThirdPersonCameraDistance(float initialDistance)
        {
            _distance = Mathf.Clamp(initialDistance, MinimumDistance, MaximumDistance);
        }

        public void SetTransitioning(bool transitioning)
        {
            _isTransitioning = transitioning;
        }

        public bool TryAddZoom(float delta)
        {
            if (_isTransitioning) return false;

            _distance = Mathf.Clamp(_distance + delta, MinimumDistance, MaximumDistance);
            return true;
        }

        public float GetDistance()
        {
            return _distance;
        }
    }
}
