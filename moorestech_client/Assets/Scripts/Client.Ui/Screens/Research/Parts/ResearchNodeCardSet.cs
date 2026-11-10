using System;
using System.Collections.Generic;
using Client.Ui.Components.NodeGraph;
using Client.Ui.Content;
using Client.Ui.State.Research.Tree;

namespace Client.Ui.Screens.Research.Parts
{
    /// <summary>
    /// 研究ノードのカードを GUID で使い回す。並びが変わったときだけグラフの組み直しを求める
    /// Reuses research node cards by GUID and asks for a graph rebuild only when the order changes
    /// </summary>
    public sealed class ResearchNodeCardSet
    {
        private readonly GameContentPresentation _content;
        private readonly Dictionary<Guid, ResearchNodeCard> _cards = new();
        private readonly List<Guid> _order = new();

        public ResearchNodeCardSet(GameContentPresentation content)
        {
            _content = content;
        }

        // 全カードを描き直し、ノードの並びが前回と違えば true
        // Redraw every card and return true when the node order differs from last time
        public bool RenderCards(IReadOnlyList<ResearchNodeCardViewModel> nodes)
        {
            var orderChanged = nodes.Count != _order.Count;
            for (var i = 0; i < nodes.Count; i++)
            {
                var node = nodes[i];
                if (!orderChanged && _order[i] != node.Guid) orderChanged = true;
                if (!_cards.TryGetValue(node.Guid, out var card))
                {
                    card = new ResearchNodeCard(node.Guid);
                    _cards.Add(node.Guid, card);
                }
                card.Render(node, _content);
            }
            if (!orderChanged) return false;
            _order.Clear();
            foreach (var node in nodes) _order.Add(node.Guid);
            return true;
        }

        public NodeGraphContent BuildContent(IReadOnlyList<ResearchNodeCardViewModel> nodes, NodeGraphFocus focus)
        {
            // キーは GUID の文字列（前提も同じ綴り）
            // Keys are GUID strings (prerequisites use the same spelling)
            var graphNodes = new List<NodeGraphNode>(nodes.Count);
            foreach (var node in nodes)
            {
                var previousKeys = new List<string>(node.PreviousGuids.Count);
                foreach (var previous in node.PreviousGuids) previousKeys.Add(previous.ToString());
                graphNodes.Add(new NodeGraphNode(node.Guid.ToString(), node.TreePosition, previousKeys, _cards[node.Guid]));
            }
            return new NodeGraphContent(graphNodes, focus);
        }
    }
}
