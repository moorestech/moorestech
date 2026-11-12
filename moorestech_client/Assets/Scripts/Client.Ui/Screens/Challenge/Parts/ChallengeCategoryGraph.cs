using Client.Ui.Components.NodeGraph;
using Client.Ui.Foundation.Input.OsCursor;
using Client.Ui.State.Challenge.Tree;

namespace Client.Ui.Screens.Challenge.Parts
{
    /// <summary>
    /// チャレンジのカテゴリ 1 つのグラフ（NodeGraphView・カードの組・最後に渡した中身）。画面の寿命で控えられ、視点は View が持つ
    /// One challenge category's graph (NodeGraphView, card set and the last content handed over); kept for the screen lifetime, the view holds the viewport
    /// </summary>
    public sealed class ChallengeCategoryGraph
    {
        private readonly ChallengeNodeCardSet _cards = new();
        private ChallengeCategoryViewModel _shown;

        public NodeGraphView View { get; }

        public ChallengeCategoryGraph(ChallengeCategoryViewModel category, IUiOsCursor osCursor)
        {
            // 作ったときに中身を渡す。部品は寸法が揃った最初の回だけ進行中へ寄せる（裁定 2026-11-04 の初回訪問）
            // Hand the content over on creation; the component centers on the current node once, when its size first arrives (first visit, ruling 2026-11-04)
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
