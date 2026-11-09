using System.Collections.Generic;

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
    }
}
