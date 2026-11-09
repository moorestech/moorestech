using Client.Ui.Foundation.Motion.Tween;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.Tutorial.DragGuide
{
    /// <summary>
    /// ドラッグ誘導の 1 周（Web の @keyframes drag-guide-loop）。現れて起点から終点へ動き、消える
    /// One loop of the drag guide (Web @keyframes drag-guide-loop): fade in, travel from origin to target, fade out
    /// </summary>
    public sealed class TutorialDragGuideStep : IUiTweenStep
    {
        private readonly VisualElement _target;
        private float _travelX;
        private float _travelY;

        public TutorialDragGuideStep(VisualElement target)
        {
            _target = target;
        }

        public void SetTravel(float x, float y)
        {
            _travelX = x;
            _travelY = y;
        }

        public void Apply(float easedProgress)
        {
            // CSS のキーフレームは区間ごとに ease-in-out が掛かるので、直線の進みを区間へ割って曲線を当てる
            // CSS applies ease-in-out per keyframe interval, so split the linear progress and ease each interval
            var p = easedProgress;
            var move = p < 0.75f ? Ease(p / 0.75f) : 1f;
            var opacity = p < 0.15f ? Ease(p / 0.15f) : p < 0.75f ? 1f : 1f - Ease((p - 0.75f) / 0.25f);
            _target.style.translate = new Translate(new Length(_travelX * move), new Length(_travelY * move));
            _target.style.opacity = opacity;

            #region Internal

            float Ease(float t)
            {
                return UiEasingFunctions.Evaluate(UiEasing.EaseInOutSine, t); // look-exception: LX-37
            }

            #endregion
        }
    }
}
