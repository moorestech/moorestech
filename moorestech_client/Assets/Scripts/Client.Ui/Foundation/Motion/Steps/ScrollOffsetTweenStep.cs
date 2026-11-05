using Client.Ui.Foundation.Motion.Tween;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Ui.Foundation.Motion.Steps
{
    /// <summary>
    /// ScrollView のスクロール位置を補間する（smooth scroll 用）
    /// Interpolates a ScrollView's scroll offset (for smooth scroll)
    /// </summary>
    public sealed class ScrollOffsetTweenStep : IUiTweenStep
    {
        private readonly ScrollView _scrollView;
        private readonly Vector2 _from;
        private readonly Vector2 _to;

        public ScrollOffsetTweenStep(ScrollView scrollView, Vector2 from, Vector2 to)
        {
            _scrollView = scrollView;
            _from = from;
            _to = to;
        }

        public void Apply(float easedProgress)
        {
            _scrollView.scrollOffset = Vector2.LerpUnclamped(_from, _to, easedProgress);
        }
    }
}
