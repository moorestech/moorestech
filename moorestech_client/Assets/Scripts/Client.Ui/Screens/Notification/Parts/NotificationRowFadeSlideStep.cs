using Client.Ui.Foundation.Motion.Steps;
using Client.Ui.Foundation.Motion.Tween;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.Notification.Parts
{
    /// <summary>
    /// Foundation の不透明度と横移動の Step を同じ進みで書く束ね（Web の opacity と translateX を 1 つの keyframes で動かすのと同じ）。自分ではスタイルを書かない
    /// Drives the Foundation opacity and horizontal-translate steps with one progress (same as the Web keyframes moving opacity and translateX together); it writes no style itself
    /// </summary>
    public sealed class NotificationRowFadeSlideStep : IUiTweenStep
    {
        private readonly OpacityTweenStep _opacity;
        private readonly TranslateXTweenStep _translateX;

        public NotificationRowFadeSlideStep(VisualElement row, float fromOpacity, float toOpacity, float fromPixels, float toPixels)
        {
            _opacity = new OpacityTweenStep(row, fromOpacity, toOpacity);
            _translateX = new TranslateXTweenStep(row, fromPixels, toPixels);
        }

        public void Apply(float easedProgress)
        {
            _opacity.Apply(easedProgress);
            _translateX.Apply(easedProgress);
        }
    }
}
