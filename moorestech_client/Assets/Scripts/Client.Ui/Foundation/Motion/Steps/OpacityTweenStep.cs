using Client.Ui.Foundation.Motion.Tween;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Ui.Foundation.Motion.Steps
{
    /// <summary>
    /// 要素の不透明度を補間する
    /// Interpolates the element's opacity
    /// </summary>
    public sealed class OpacityTweenStep : IUiTweenStep
    {
        private readonly VisualElement _target;
        private readonly float _from;
        private readonly float _to;

        public OpacityTweenStep(VisualElement target, float from, float to)
        {
            _target = target;
            _from = from;
            _to = to;
        }

        public void Apply(float easedProgress)
        {
            _target.style.opacity = Mathf.LerpUnclamped(_from, _to, easedProgress);
        }
    }
}
