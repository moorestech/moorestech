using Client.Game.InGame.Tutorial.UIHighlight;
using Client.Ui.Foundation.Anchor;
using Client.Ui.Foundation.Input;
using Client.Ui.Foundation.Layout;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.CommonHud
{
    /// <summary>
    /// 舞台の中央の点（Web の Crosshair）。影の輪を奥に、白い円を手前に重ねる
    /// The dot at the stage center (Web Crosshair), with the shadow ring behind and the white circle in front
    /// </summary>
    public sealed class CrosshairHudView : VisualElement
    {
        private readonly VisualElement _crosshair;

        public CrosshairHudView()
        {
            AddToClassList("crosshair-hud");
            _crosshair = new VisualElement { name = "crosshair" };
            _crosshair.AddToClassList("crosshair-hud__crosshair");
            TutorialAnchor.Mark(_crosshair, AnchorIds.GameCrosshair);
            _crosshair.Add(new CrosshairShadowRing());
            var dot = new VisualElement();
            dot.AddToClassList("crosshair-hud__dot");
            _crosshair.Add(dot);
            Add(_crosshair);

            // 常駐 HUD はどの要素も入力を奪わない（Web の pointer-events: none）
            // No element of a resident HUD takes input (Web pointer-events: none)
            UiPickingIgnore.ApplyToSubtree(this);
            SetShown(false);
        }

        public void SetShown(bool shown)
        {
            UiDisplay.SetShown(_crosshair, shown);
        }
    }
}
