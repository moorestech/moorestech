using Client.Game.InGame.Tutorial.UIHighlight;
using Client.Ui.Components.Gauges;
using Client.Ui.Foundation.Anchor;
using Client.Ui.Foundation.Input;
using Client.Ui.Foundation.Layout;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.Progress
{
    /// <summary>
    /// ホットバーの床の上に積む採掘進捗のゲージ 1 本（Web の ProgressBar）。ラベルは源が無いので持たない
    /// One mining-progress gauge stacked on the hotbar floor (Web ProgressBar); no label because nothing supplies one
    /// </summary>
    public sealed class ProgressHudView : VisualElement
    {
        private readonly VisualElement _column;
        private readonly GaugeBar _gauge;

        public ProgressHudView()
        {
            AddToClassList("progress-hud");
            _column = new VisualElement { name = "progress-bar" };
            _column.AddToClassList("progress-hud__column");
            TutorialAnchor.Mark(_column, AnchorIds.MiningHud);
            _gauge = new GaugeBar("progress-gauge");
            _column.Add(_gauge);
            Add(_column);

            // 常駐 HUD はどの要素も入力を奪わない（Web の pointer-events: none）
            // No element of a resident HUD takes input (Web pointer-events: none)
            UiPickingIgnore.ApplyToSubtree(this);
            SetShown(false);
        }

        public void SetShown(bool shown)
        {
            UiDisplay.SetShown(_column, shown);
        }

        public void SetProgress(float progress01)
        {
            _gauge.SetValue(progress01);
        }
    }
}
