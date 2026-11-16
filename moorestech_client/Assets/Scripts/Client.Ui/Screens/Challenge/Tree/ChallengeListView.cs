using System;
using Client.Game.InGame.Tutorial.UIHighlight;
using Client.Ui.Foundation.Anchor;
using Client.Ui.Foundation.Input.OsCursor;
using Client.Ui.Foundation.Layout;
using Client.Ui.Foundation.Text;
using Client.Ui.Screens.Challenge.Parts;
using Client.Ui.State.Challenge.Commands;
using Client.Ui.State.Challenge.Tree;
using Client.Ui.State.Text;
using Mooresmaster.Localization.Generated;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.Challenge.Tree
{
    /// <summary>
    /// チャレンジ一覧（題・ボタン列・グラフ）。web: moorestech_web/webui/src/features/challenge/ChallengePanel.tsx
    /// Challenge list (title, buttons, graph); web: moorestech_web/webui/src/features/challenge/ChallengePanel.tsx
    /// </summary>
    public sealed class ChallengeListView : VisualElement
    {
        private readonly ChallengeCategoryButtons _categories;
        private readonly ChallengeCategoryGraphHost _graphHost;

        public ChallengeListView(IChallengeListCommands commands, IUiOsCursor osCursor)
        {
            // 根は一覧の面。拾うので一覧の上の押下とホイールは UI に当たる（右短押しで閉じず、ホットバーへ抜けない）
            // The root is the list face; it picks, so presses and wheels over the list hit UI (no close on a right short press, no wheel to the hotbar)
            name = "challenge-panel";
            AddToClassList("challenge-list");
            TutorialAnchor.Mark(this, AnchorIds.ChallengePanel);

            // 題・カテゴリ列・グラフ置き場を縦に積む
            // Stack title, category row and graph host
            var title = new LocalizedLabel();
            title.AddToClassList(UiTypographyClass.TitleOrder2);
            title.AddToClassList("challenge-list__title");
            title.SetText(new UiText(LocalizationKeys.Ui.Challenge.Title));
            Add(title);
            _categories = new ChallengeCategoryButtons(commands);
            Add(_categories);
            _graphHost = new ChallengeCategoryGraphHost(osCursor);
            Add(_graphHost);
        }

        public void Render(ChallengeListViewModel model)
        {
            // 空か選択ありかの分岐はここの 1 か所。部品は null を見ない
            // The only branch between empty and selected lives here; the parts never see null
            switch (model)
            {
                case SelectedChallengeListViewModel selected:
                    _categories.Render(selected);
                    _graphHost.Render(selected.Selected);
                    break;
                case EmptyChallengeListViewModel:
                    // 解放済みが無ければ題だけ
                    // Only the title without unlocked categories
                    _categories.ClearButtons();
                    _graphHost.UnmountGraph();
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(model), model.GetType().Name, null);
            }
        }

        public void SetShown(bool shown)
        {
            // 隠す間も要素を残し、控えた各カテゴリの View の視点を保つ（Foundation の UiDisplay があればその 1 行に替える）
            // Keep the element while hidden so every kept category view keeps its viewport (replace with the Foundation UiDisplay line when it exists)
            UiDisplay.SetShown(this, shown);
        }
    }
}
