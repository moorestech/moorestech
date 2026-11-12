using Client.Game.InGame.Tutorial.UIHighlight;
using Client.Localization;
using Client.Ui.Components.Panels;
using Client.Ui.Foundation.Anchor;
using Client.Ui.Foundation.Input;
using Client.Ui.Foundation.Layout;
using Client.Ui.Foundation.Text;
using Client.Ui.State.Routing;
using Client.Ui.State.Text;
using Client.Ui.State.Train;
using Mooresmaster.Localization.Generated;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.TrainHud
{
    /// <summary>
    /// 舞台の下中央の列車乗車 HUD（Web の TrainRidingHud）。Paper の面に題と選択中の分岐を縦に並べる
    /// Train riding HUD at the stage bottom center (Web TrainRidingHud): the title and selected branch stacked on a Paper face
    /// </summary>
    public sealed class TrainRidingHudView : VisualElement
    {
        private readonly PaperSurface _paper;
        private readonly UiStack _stack;
        private readonly LocalizedLabel _branch;

        public TrainRidingHudView()
        {
            AddToClassList("train-riding-hud");
            _paper = new PaperSurface("train-riding-hud");
            _paper.AddToClassList("train-riding-hud__paper");
            TutorialAnchor.Mark(_paper, AnchorIds.TrainHudStatus);

            // Stack gap 4px に題（Title order 2・size h4）と分岐（Text）
            // Title (Title order 2, size h4) and branch (Text) in a 4px-gap stack
            _stack = new UiStack(UiGap.Px4);
            var title = new LocalizedLabel();
            title.AddToClassList(UiTypographyClass.TitleOrder4);
            title.AddToClassList(UiTypographyClass.TitleHeadingFace);
            title.AddToClassList("train-riding-hud__title");
            title.SetKey(LocalizationKeys.Ui.TrainHud.Title);
            _stack.AddItem(title);
            _branch = new LocalizedLabel { name = "train-branch-selection" };
            _branch.AddToClassList(UiTypographyClass.TextMd);
            _branch.AddToClassList("train-riding-hud__branch");
            _stack.AddItem(_branch);
            _paper.Add(_stack);
            Add(_paper);
            Render(TrainRidingHudViewModel.Create(UiScreen.None, false, 0, 0));
        }

        public void Render(TrainRidingHudViewModel model)
        {
            _paper.style.display = model.Shown ? DisplayStyle.Flex : DisplayStyle.None;

            // 隠す間も文は入れておく（文の無い LocalizedLabel は載った 1 フレーム後に LogError を出す）
            // Keep a text even while hidden (a LocalizedLabel without one logs an error a frame after attaching)
            var current = new LocalizationTextParam("current", model.SelectedBranchNumber.ToString());
            var count = new LocalizationTextParam("count", model.BranchCandidateCount.ToString());
            _branch.SetText(new UiText(LocalizationKeys.Ui.TrainHud.BranchSelection, new[] { current, count }));
            _stack.SetItemShown(_branch, model.BranchShown);

            // 常駐 HUD はどの要素も入力を奪わない（Web の pointer-events: none）。SetItemShown が作り直す包みも含める
            // No element of a resident HUD takes input (Web pointer-events: none), including wrappers SetItemShown rebuilds
            UiPickingIgnore.ApplyToSubtree(this);
        }
    }
}
