using UnityEngine;

namespace Client.Ui.Components.NodeGraph
{
    /// <summary>
    /// ドラッグ中の速度を追い、離したときに滑走するかを決める。web: moorestech_web/webui/src/shared/treeView/viewport/usePanInertia.ts の trackMove・release
    /// Tracks drag velocity and decides on release whether to glide; web: trackMove and release in moorestech_web/webui/src/shared/treeView/viewport/usePanInertia.ts
    /// </summary>
    internal sealed class NodeGraphPanInertiaTracker
    {
        private Vector2 _velocity;
        private long _lastMoveAtMs;
        private bool _hasLastMove;

        public void Reset()
        {
            _velocity = Vector2.zero;
            _hasLastMove = false;
        }

        public void TrackMove(Vector2 delta, long nowMs)
        {
            // 同じフレームの 2 つ目の移動（間隔 0）は標本にせず速度を保つ（D-7）
            // A second move in the same frame (zero gap) is not a sample and keeps the velocity (D-7)
            if (_hasLastMove && nowMs == _lastMoveAtMs) return;

            // 最初の標本・逆行・80ms 超の間隔は速度を作らない（Web と同じ）
            // The first sample, a backward clock or a gap over 80 ms builds no velocity (same as the Web)
            var hadLastMove = _hasLastMove;
            var gapMs = nowMs - _lastMoveAtMs;
            _lastMoveAtMs = nowMs;
            _hasLastMove = true;
            if (!hadLastMove || gapMs < 0 || gapMs > NodeGraphPanVelocity.SampleMaxGapMs)
            {
                _velocity = Vector2.zero;
                return;
            }
            _velocity = NodeGraphPanVelocity.Blend(_velocity, delta, gapMs);
        }

        public bool TryStartFling(long releasedAtMs, out Vector2 velocity)
        {
            // 止まってから離した・遅すぎる速度は滑走させない
            // No glide after holding still before release, or below the fling speed
            velocity = NodeGraphPanVelocity.Clamp(_velocity);
            if (!_hasLastMove || releasedAtMs - _lastMoveAtMs > NodeGraphPanVelocity.ReleaseStallMs) return false;
            return velocity.magnitude >= NodeGraphPanVelocity.MinFlingSpeed;
        }
    }
}
