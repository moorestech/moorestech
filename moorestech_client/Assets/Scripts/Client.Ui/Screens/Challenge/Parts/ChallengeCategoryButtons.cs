using System;
using System.Collections.Generic;
using Client.Game.InGame.Tutorial.UIHighlight;
using Client.Ui.Components.Controls;
using Client.Ui.Foundation.Anchor;
using Client.Ui.Foundation.Layout;
using Client.Ui.State.Challenge.Commands;
using Client.Ui.State.Challenge.Tree;
using Client.Ui.State.Text;
using Mooresmaster.Localization.Generated;
using UniRx;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.Challenge.Parts
{
    /// <summary>
    /// チャレンジのカテゴリのボタン列（Web の Group gap md・nowrap・overflow-x: auto と Button filled/subtle）
    /// Challenge category button row (the Web Group gap md, nowrap, overflow-x: auto and Button filled/subtle)
    /// </summary>
    public sealed class ChallengeCategoryButtons : VisualElement
    {
        private readonly IChallengeListCommands _commands;
        private readonly UiGroup _group = new(UiGap.Md);
        private readonly Dictionary<Guid, FormButton> _buttons = new();
        private IReadOnlyList<Guid> _order = Array.Empty<Guid>();

        public ChallengeCategoryButtons(IChallengeListCommands commands)
        {
            // 列の根がアンカー。行は折り返さず、越えたら横に送る
            // The row root carries the anchor; the row never wraps and scrolls sideways on overflow
            _commands = commands;
            name = "challenge-categories";
            AddToClassList("challenge-list__categories");
            _group.AddToClassList(UiLayoutClass.GroupNoWrap);
            _group.AddToClassList("challenge-list__category-group");
            var scroll = new GameScrollView("challenge-categories-scroll", GameScrollAxis.Horizontal);
            scroll.Add(_group);
            Add(scroll);
            TutorialAnchor.Mark(this, AnchorIds.ChallengeCategories);
        }

        public void Render(SelectedChallengeListViewModel model)
        {
            // カテゴリの並びが変わったときだけボタンを作り直す（並びの比較は State の 1 本）
            // Rebuild the buttons only when the category order changes (the order comparison is the single one in State)
            if (!ChallengeListLogic.SameGuidSequence(_order, model.CategoryGuids)) Rebuild(model);

            // 選んだカテゴリは filled、他は subtle
            // The selected category is filled, the others subtle
            foreach (var category in model.Categories)
            {
                var isSelected = category.Guid == model.Selected.Guid;
                _buttons[category.Guid].SetVariant(isSelected ? FormButtonVariant.Filled : FormButtonVariant.Subtle);
            }
        }

        public void ClearButtons()
        {
            // 解放済みのカテゴリが無い一覧はボタンを持たない
            // A list without unlocked categories holds no buttons
            _group.ClearItems();
            _buttons.Clear();
            _order = Array.Empty<Guid>();
        }

        private void Rebuild(SelectedChallengeListViewModel model)
        {
            // 文言はマスタの内容キー（A-40）。押すとカテゴリを選ぶ。ボタンは縮めない
            // Labels are master content keys (A-40); pressing selects the category; buttons never shrink
            ClearButtons();
            foreach (var category in model.Categories)
            {
                var guid = category.Guid;
                var button = new FormButton($"challenge-category-{guid}", UiText.FromContent(ContentLocalizationKeys.ChallengeCategoryName(guid)), FormButtonVariant.Subtle);
                button.AddToClassList("challenge-list__category-button");
                button.OnClicked.Subscribe(_ => _commands.SelectCategory(guid));
                _group.AddItem(button);
                _buttons.Add(guid, button);
            }
            _order = model.CategoryGuids;
        }
    }
}
