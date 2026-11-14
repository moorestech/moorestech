using Client.Ui.Foundation.Input;
using Client.Ui.Foundation.Layout;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.Shell
{
    /// <summary>
    /// 実画面いっぱいの最奥の置き場。常時のヴィネットと、画面を開いている間に世界を沈める幕 screen-backdrop を持つ（Web App.tsx の .viewport と .backdrop）
    /// Back-most full-viewport holder with the persistent vignette and the screen-backdrop dim that sinks the world while a screen is open (Web App.tsx .viewport and .backdrop)
    /// </summary>
    public sealed class ScreenBackdropView : VisualElement
    {
        private readonly VisualElement _backdrop;

        public ScreenBackdropView()
        {
            AddToClassList("screen-shell");

            // ヴィネットを奥に、幕をその手前に置く（Web は幕が .viewport の背景の上に重なる）
            // Vignette at the back and the dim in front (on the Web the dim lies over the .viewport background)
            Add(new ScreenVignette());
            _backdrop = new VisualElement { name = "screen-backdrop" };
            _backdrop.AddToClassList("screen-shell__backdrop");
            UiDisplay.SetShown(_backdrop, false);
            Add(_backdrop);

            // 部分木ごと入力を取らない（Web の .viewport と .backdrop は pointer-events: none）
            // The whole subtree takes no input (the Web .viewport and .backdrop are pointer-events: none)
            UiPickingIgnore.ApplyToSubtree(this);
        }

        // 受け身。どの画面で出すかは ScreenBackdropPresenter が決める（設計書 §4.4・A-44）
        // Passive; ScreenBackdropPresenter decides on which screens it shows (design §4.4, A-44)
        public void SetBackdropShown(bool shown)
        {
            UiDisplay.SetShown(_backdrop, shown);
        }
    }
}
