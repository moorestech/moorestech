using System;
using System.Collections.Generic;
using Client.Ui.Components.NodeGraph;
using Client.Ui.Content;
using Client.Ui.State.Research.Tree;

namespace Client.Ui.Screens.Research.Parts
{
    /// <summary>
    /// ノードカードをGUIDで使い回す。同じ形なら組み直さない判定は NodeGraphContent.HasSameShapeAs が持つ（並びの比較をここに置かない）
    /// Reuses node cards by GUID; whether to skip the rebuild for the same shape is decided by NodeGraphContent.HasSameShapeAs (no order comparison here)
    /// </summary>
    public sealed class ResearchNodeCardSet
    {
        private readonly GameContentPresentation _content;
        private readonly Dictionary<Guid, ResearchNodeCard> _cards = new();

        public ResearchNodeCardSet(GameContentPresentation content)
        {
            _content = content;
        }

        // 全カードを描き直す（無いカードは作る）
        // Redraw every card, creating missing ones
        public void RenderCards(IReadOnlyList<ResearchNodeCardViewModel> nodes)
        {
            foreach (var node in nodes)
            {
                if (!_cards.TryGetValue(node.Guid, out var card))
                {
                    card = new ResearchNodeCard(node.Guid);
                    _cards.Add(node.Guid, card);
                }
                card.Render(node, _content);
            }
        }

        public NodeGraphContent BuildContent(IReadOnlyList<ResearchNodeCardViewModel> nodes, NodeGraphFocus focus)
        {
            // キーはGUID文字列
            // Keys are GUID strings
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
