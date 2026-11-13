using Client.Ui.Components.NodeGraph;
using Client.Ui.Foundation.Input.OsCursor;
using Client.Ui.State.Challenge.Tree;

namespace Client.Ui.Screens.Challenge.Parts
{
    /// <summary>
    /// カテゴリ 1 つのグラフ（View・カード・最後の中身）
    /// One category's graph (view, cards, last content)
    /// </summary>
    public sealed class ChallengeCategoryGraph
    {
        private readonly ChallengeNodeCardSet _cards = new();
        private ChallengeCategoryViewModel _shown;

        public NodeGraphView View { get; }

        public ChallengeCategoryGraph(ChallengeCategoryViewModel category, IUiOsCursor osCursor)
        {
            // 作成時に中身を渡す（初回訪問は裁定 2026-11-04）
            // Hand content over on creation (first visit, ruling 2026-11-04)
            View = new NodeGraphView("challenge", osCursor);
            _shown = category;
            _cards.RenderCards(category.Nodes);
            View.SetGraph(BuildContent(category));
        }

        public void Render(ChallengeCategoryViewModel category)
        {
            // カードは毎回描き直す（状態・倍率）。渡す中身（並び・座標・線・注目点）が変わったときだけ渡し直す（D-20）
            // Cards are redrawn every time (state, scale); content is handed over only when it changes (order, positions, edges, focus; D-20)
            _cards.RenderCards(category.Nodes);
            var contentChanged = !category.HasSameGraphAs(_shown);
            _shown = category;
            // silent-ok: 中身が同じなら渡し直さない（部品の作り直しを避ける） / カードの描き直しは上で済み、L1 ChallengeListViewportTest が視点の保持を見る
            if (!contentChanged) return;
            View.SetGraph(BuildContent(category));
        }

        private NodeGraphContent BuildContent(ChallengeCategoryViewModel category)
        {
            var hasFocus = category.TryGetInitialFocus(out var focus);
            return _cards.BuildContent(category.Nodes, hasFocus ? NodeGraphFocus.At(focus) : NodeGraphFocus.None);
        }
    }
}
