using System.Collections.Generic;
using Client.UitkPoc.RecipeGraph.Model;

namespace Client.UitkPoc.RecipeGraph.View
{
    // ホバー中ノードの上流（原料側）と下流（目標側）をすべて集める
    // Collects every upstream (raw side) and downstream (target side) element of the hovered node
    public static class RecipeGraphHighlight
    {
        public static (HashSet<int> nodes, HashSet<int> edges) Collect(ProductionGraph graph, int focusNode)
        {
            var nodes = new HashSet<int> { focusNode };
            var edges = new HashSet<int>();
            Walk(focusNode, true);
            Walk(focusNode, false);
            return (nodes, edges);

            #region Internal

            void Walk(int start, bool upstream)
            {
                var stack = new Stack<int>();
                stack.Push(start);
                while (stack.Count > 0)
                {
                    var current = stack.Pop();
                    for (var e = 0; e < graph.Edges.Count; e++)
                    {
                        var edge = graph.Edges[e];
                        var next = upstream ? (edge.ToNode == current ? edge.FromNode : -1) : (edge.FromNode == current ? edge.ToNode : -1);
                        if (next < 0) continue;
                        edges.Add(e);
                        if (nodes.Add(next)) stack.Push(next);
                    }
                }
            }

            #endregion
        }
    }
}
