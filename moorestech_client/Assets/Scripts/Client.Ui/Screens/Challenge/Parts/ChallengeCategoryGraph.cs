using Client.Ui.Components.NodeGraph;
using Client.Ui.Foundation.Input.OsCursor;
using Client.Ui.State.Challenge.Tree;

namespace Client.Ui.Screens.Challenge.Parts
{
    /// <summary>
    /// カテゴリ 1 つのグラフ（View・カード）
    /// One category's graph (view, cards)
    /// </summary>
    public sealed class ChallengeCategoryGraph
    {
        private readonly ChallengeNodeCardSet _cards = new();

        public NodeGraphView View { get; }

        public ChallengeCategoryGraph(ChallengeCategoryViewModel category, IUiOsCursor osCursor)
        {
            // 作成時に中身を渡す（初回訪問は裁定 2026-11-04）
            // Hand content over on creation (first visit, ruling 2026-11-04)
            View = new NodeGraphView("challenge", osCursor);
            _cards.RenderCards(category.Nodes);
            View.SetGraph(BuildContent(category));
        }

        public void Render(ChallengeCategoryViewModel category)
        {
            // カードは毎回描き直す（状態・倍率）。中身も毎回渡し、同じ形なら組み直さない判定は NodeGraphView.SetGraph が持つ（D-20）
            // Cards are redrawn every time (state, scale); content is handed every time and NodeGraphView.SetGraph skips the rebuild for the same shape (D-20)
            _cards.RenderCards(category.Nodes);
            View.SetGraph(BuildContent(category));
        }

        private NodeGraphContent BuildContent(ChallengeCategoryViewModel category)
        {
            var hasFocus = category.TryGetInitialFocus(out var focus);
            return _cards.BuildContent(category.Nodes, hasFocus ? NodeGraphFocus.At(focus) : NodeGraphFocus.None);
        }
    }
}
