using System;
using System.Collections.Generic;
using Client.Ui.Foundation.Input.OsCursor;
using Client.Ui.State.Challenge.Tree;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.Challenge.Parts
{
    /// <summary>
    /// 選んだカテゴリのグラフを置く。訪れたカテゴリごとに NodeGraphView を 1 本控え、選択が変わったら置く View を付け替える（Web の key={selected.guid} と viewportStore）
    /// Hosts the selected category's graph; keeps one NodeGraphView per visited category and swaps the attached view on selection changes (the Web key={selected.guid} and viewportStore)
    /// </summary>
    /// <remarks>
    /// 視点は View の寿命で保たれる（I-fix6 D-3）。View を作ったときだけ部品が進行中へ 1 回寄せ、再訪と開き直しは控えた View の視点のまま（裁定 2026-11-04）。置き場の子は表示中のカテゴリの View 1 本だけなので、名前 challenge-viewport とカードのアンカーは木の中で重ならない
    /// The viewport lives as long as the view (I-fix6 D-3); the component centers once only when a view is created, and revisits and reopens keep that view's viewport (ruling 2026-11-04); the host holds only the shown category's view, so the challenge-viewport name and card anchors never repeat in the tree
    /// </remarks>
    public sealed class ChallengeCategoryGraphHost : VisualElement
    {
        private readonly IUiOsCursor _osCursor;
        private readonly Dictionary<Guid, ChallengeCategoryGraph> _graphs = new();

        public ChallengeCategoryGraphHost(IUiOsCursor osCursor)
        {
            _osCursor = osCursor;
            AddToClassList("challenge-list__graph-host");
            pickingMode = PickingMode.Ignore;
        }

        public void Render(ChallengeCategoryViewModel category)
        {
            // 初めて訪れたカテゴリは View を作る（作った View だけが進行中へ寄せる）。訪れたことがあれば中身を描き直す
            // A first visit creates a view (only a new view centers on the current node); a visited category redraws its content
            if (_graphs.TryGetValue(category.Guid, out var graph)) graph.Render(category);
            else
            {
                graph = new ChallengeCategoryGraph(category, _osCursor);
                _graphs.Add(category.Guid, graph);
            }

            // 表示中のカテゴリの View だけを置く。前のカテゴリの View は外して控え、視点を持ったまま待つ
            // Attach only the shown category's view; the previous one is detached and kept with its viewport
            // silent-ok: 既に置いてあれば付け替えない / 正常系（同じカテゴリの描き直し）。付け替えと視点は L1 ChallengeListViewportTest が見る
            if (graph.View.parent == this) return;
            Clear();
            Add(graph.View);
        }

        public void UnmountGraph()
        {
            // 解放済みのカテゴリが無い一覧はグラフを置かない（控えた View は残す）
            // A list without unlocked categories attaches no graph (kept views stay)
            Clear();
        }
    }
}
