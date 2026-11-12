using System;
using Client.Game.InGame.Tutorial.UIHighlight;
using Client.Ui.Foundation.Anchor;
using Client.Ui.Foundation.Input.OsCursor;
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
    /// チャレンジ一覧（題・カテゴリのボタン列・ノードグラフ）。web: moorestech_web/webui/src/features/challenge/ChallengePanel.tsx
    /// Challenge list (title, category buttons, node graph); web: moorestech_web/webui/src/features/challenge/ChallengePanel.tsx
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

            // 題（Title order 2）・カテゴリ列・グラフの置き場を縦に積む
            // Stack the title (Title order 2), the category row and the graph host
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
                    // 解放済みのカテゴリが無ければ題だけ（Web は selected が無いと TreeView を描かない）
                    // Without an unlocked category only the title remains (the Web skips TreeView without a selection)
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
            style.display = shown ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
}
