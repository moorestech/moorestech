using Client.Ui.Foundation.Motion.Tween;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Ui.Foundation.Motion.Steps
{
    /// <summary>
    /// 要素の拡大率を等方に補間する
    /// Interpolates the element's scale isotropically
    /// </summary>
    public sealed class ScaleTweenStep : IUiTweenStep
    {
        private readonly VisualElement _target;
        private readonly float _from;
        private readonly float _to;

        public ScaleTweenStep(VisualElement target, float from, float to)
        {
            _target = target;
            _from = from;
            _to = to;
        }

        public void Apply(float easedProgress)
        {
            var scale = Mathf.LerpUnclamped(_from, _to, easedProgress);
            _target.style.scale = new Scale(new Vector3(scale, scale, 1f));
        }
    }

}
