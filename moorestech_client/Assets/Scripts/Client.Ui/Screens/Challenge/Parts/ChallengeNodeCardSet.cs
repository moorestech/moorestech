using System;
using System.Collections.Generic;
using Client.Ui.Components.NodeGraph;
using Client.Ui.State.Challenge.Tree;

namespace Client.Ui.Screens.Challenge.Parts
{
    /// <summary>
    /// チャレンジのカードを GUID で使い回し、グラフの中身を組む（前例 ResearchNodeCardSet）
    /// Reuses challenge cards by GUID and builds the graph content (precedent ResearchNodeCardSet)
    /// </summary>
    /// <remarks>
    /// 中身を渡し直すかの判定は State の ChallengeCategoryViewModel.HasSameGraphAs が持つ（並びの比較をここに置かない）
    /// Whether to hand content over again is decided by State's ChallengeCategoryViewModel.HasSameGraphAs (no order comparison here)
    /// </remarks>
    public sealed class ChallengeNodeCardSet
    {
        private readonly Dictionary<Guid, ChallengeNodeCard> _cards = new();

        // 全カードを描き直す。初めての GUID はカードを作る
        // Redraw every card; a new GUID gets a new card
        public void RenderCards(IReadOnlyList<ChallengeNodeCardViewModel> nodes)
        {
            foreach (var node in nodes)
            {
                if (!_cards.TryGetValue(node.Guid, out var card))
                {
                    card = new ChallengeNodeCard(node.Guid);
                    _cards.Add(node.Guid, card);
                }
                card.Render(node);
            }
        }

        public NodeGraphContent BuildContent(IReadOnlyList<ChallengeNodeCardViewModel> nodes, NodeGraphFocus focus)
        {
            // キーは GUID の文字列。前提は同じカテゴリのものだけが来る（ChallengeCategoryViewModel）
            // Keys are GUID strings; only same-category prerequisites arrive (ChallengeCategoryViewModel)
            var graphNodes = new List<NodeGraphNode>(nodes.Count);
            foreach (var node in nodes)
            {
                var previousKeys = new List<string>(node.PreviousGuids.Count);
                foreach (var previous in node.PreviousGuids) previousKeys.Add(previous.ToString());
                graphNodes.Add(new NodeGraphNode(node.Guid.ToString(), node.TreePosition, previousKeys, _cards[node.Guid].Root));
            }
            return new NodeGraphContent(graphNodes, focus);
        }
    }
}
