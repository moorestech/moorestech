using System.Collections.Generic;
using System.Linq;

namespace Client.Ui.Components.NodeGraph
{
    /// <summary>
    /// ノードグラフへ渡す入力（ノードの列と初回の注目点）。Web の TreeView の nodes・initialFocus
    /// Input passed to the node graph (the nodes and the initial focus); the Web TreeView nodes and initialFocus
    /// </summary>
    public sealed class NodeGraphContent
    {
        public IReadOnlyList<NodeGraphNode> Nodes { get; }
        public NodeGraphFocus InitialFocus { get; }

        public NodeGraphContent(IReadOnlyList<NodeGraphNode> nodes, NodeGraphFocus initialFocus)
        {
            Nodes = nodes;
            InitialFocus = initialFocus;
        }

        // 組み直しの要らない同じ形か（並び・キー・座標・前提・カード・注目点）。研究とチャレンジが共有する唯一の基準
        // Whether the shape needs no rebuild (order, keys, positions, prerequisites, cards, focus); the single criterion shared by research and challenges
        public bool HasSameShapeAs(NodeGraphContent other)
        {
            if (Nodes.Count != other.Nodes.Count) return false;
            for (var i = 0; i < Nodes.Count; i++)
            {
                if (!HasSamePlacement(Nodes[i], other.Nodes[i])) return false;
            }
            var hasFocus = InitialFocus.TryGetPoint(out var focus);
            var otherHasFocus = other.InitialFocus.TryGetPoint(out var otherFocus);
            return hasFocus == otherHasFocus && focus == otherFocus;

            #region Internal

            bool HasSamePlacement(NodeGraphNode node, NodeGraphNode otherNode)
            {
                // カードの差し替えも組み直しが要るので要素の同一性まで見る
                // Swapping a card also needs a rebuild, so compare element identity too
                return node.Key == otherNode.Key && node.TreePosition == otherNode.TreePosition && node.PreviousKeys.SequenceEqual(otherNode.PreviousKeys) && ReferenceEquals(node.Card, otherNode.Card);
            }

            #endregion
        }
    }
}
