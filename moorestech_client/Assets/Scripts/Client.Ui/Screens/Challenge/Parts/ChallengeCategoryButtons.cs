using System;
using System.Collections.Generic;
using System.Linq;
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
    /// チャレンジのカテゴリのボタン列
    /// Challenge category button row
    /// </summary>
    public sealed class ChallengeCategoryButtons : VisualElement
    {
        private readonly IChallengeListCommands _commands;
        private readonly UiGroup _group = new(UiGap.Md);
        private readonly Dictionary<Guid, FormButton> _buttons = new();
        private IReadOnlyList<Guid> _order = Array.Empty<Guid>();

        public ChallengeCategoryButtons(IChallengeListCommands commands)
        {
            // 根がアンカー。折り返さず横へ送る
            // Root carries the anchor; scrolls sideways without wrapping
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
            // 並びが変わったときだけ作り直す
            // Rebuild only when the order changes
            if (!_order.SequenceEqual(model.CategoryGuids)) Rebuild();

            // 選択は filled、他は subtle
            // Selected is filled, others subtle
            foreach (var category in model.Categories)
            {
                var isSelected = category.Guid == model.Selected.Guid;
                _buttons[category.Guid].SetVariant(isSelected ? FormButtonVariant.Filled : FormButtonVariant.Subtle);
            }

            #region Internal

            void Rebuild()
            {
                // 文言はマスタ内容キー（A-40）。押すと選択
                // Labels are master content keys (A-40); pressing selects
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

            #endregion
        }

        public void ClearButtons()
        {
            // 解放済みが無ければボタン無し
            // No buttons without unlocked categories
            _group.ClearItems();
            _buttons.Clear();
            _order = Array.Empty<Guid>();
        }
    }
}
