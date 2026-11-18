using Client.Ui.Foundation.Motion.Tween;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Ui.Foundation.Motion.Steps
{
    /// <summary>
    /// 要素の横の translate を px で補間する（縦は現在値を保つ）
    /// Interpolates the element's horizontal translate in pixels (vertical keeps its current value)
    /// </summary>
    public sealed class TranslateXTweenStep : IUiTweenStep
    {
        private readonly VisualElement _target;
        private readonly float _fromPixels;
        private readonly float _toPixels;

        public TranslateXTweenStep(VisualElement target, float fromPixels, float toPixels)
        {
            _target = target;
            _fromPixels = fromPixels;
            _toPixels = toPixels;
        }

        public void Apply(float easedProgress)
        {
            _target.style.translate = new Translate(Mathf.LerpUnclamped(_fromPixels, _toPixels, easedProgress), _target.style.translate.value.y);
        }
    }
}
