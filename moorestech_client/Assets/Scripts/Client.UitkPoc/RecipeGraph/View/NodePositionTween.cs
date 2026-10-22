using UnityEngine;

namespace Client.UitkPoc.RecipeGraph.View
{
    // 再レイアウト時に旧位置から新位置へノードを滑らせる
    // Slides nodes from old to new positions after a relayout
    public class NodePositionTween
    {
        private const float DurationSeconds = 0.45f;

        private Vector2[] _from = new Vector2[0];
        private Vector2[] _to = new Vector2[0];
        private float _elapsed = DurationSeconds;

        public bool IsRunning => _elapsed < DurationSeconds;

        public void Start(Vector2[] from, Vector2[] to)
        {
            _from = from;
            _to = to;
            _elapsed = 0f;
        }

        // 経過を進めて現在位置をcurrentへ書く。easeOutCubic
        // Advance and write current positions into current, easeOutCubic
        public void Advance(float deltaSeconds, Vector2[] current)
        {
            _elapsed = Mathf.Min(DurationSeconds, _elapsed + deltaSeconds);
            var t = 1f - Mathf.Pow(1f - _elapsed / DurationSeconds, 3f);
            for (var i = 0; i < current.Length; i++) current[i] = Vector2.LerpUnclamped(_from[i], _to[i], t);
        }
    }
}
