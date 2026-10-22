using System.Collections.Generic;
using System.Linq;
using Client.UitkPoc.RecipeGraph.Model;
using UnityEngine;

namespace Client.UitkPoc.RecipeGraph.View
{
    // ノード位置から各辺の通過点列とラベル位置を求める。ポートは相手の高さ順に辺を縦へ散らす
    // Computes each edge's point chain and label anchor; ports spread edges vertically by the peer's height
    public class RecipeEdgeRouter
    {
        public readonly List<Vector2>[] Paths;
        public readonly Vector2[] LabelAnchors;

        public RecipeEdgeRouter(int edgeCount)
        {
            Paths = Enumerable.Range(0, edgeCount).Select(_ => new List<Vector2>()).ToArray();
            LabelAnchors = new Vector2[edgeCount];
        }

        public void Route(ProductionGraph graph, Vector2[] nodePositions, List<Vector2>[] waypoints, bool[] manuallyMoved)
        {
            var outPort = new Dictionary<int, float>();
            var inPort = new Dictionary<int, float>();
            for (var v = 0; v < graph.Nodes.Count; v++) AssignPorts(v);

            for (var e = 0; e < graph.Edges.Count; e++)
            {
                var edge = graph.Edges[e];
                var path = Paths[e];
                path.Clear();
                path.Add(new Vector2(nodePositions[edge.FromNode].x + RecipeNodeElement.Width, outPort[e]));
                // 手で動かしたノードに繋がる辺は中継点を捨てて直結する
                // Edges touching a hand-moved node drop dummies and connect directly
                var keepWaypoints = !manuallyMoved[edge.FromNode] && !manuallyMoved[edge.ToNode];
                if (keepWaypoints) path.AddRange(waypoints[e]);
                path.Add(new Vector2(nodePositions[edge.ToNode].x, inPort[e]));
                LabelAnchors[e] = path.Count > 2 ? path[path.Count / 2] : (path[0] + path[1]) / 2f;
            }

            #region Internal

            void AssignPorts(int v)
            {
                var top = nodePositions[v].y;
                var outgoing = Enumerable.Range(0, graph.Edges.Count).Where(e => graph.Edges[e].FromNode == v).OrderBy(e => FirstHopY(e)).ToList();
                var incoming = Enumerable.Range(0, graph.Edges.Count).Where(e => graph.Edges[e].ToNode == v).OrderBy(e => LastHopY(e)).ToList();
                for (var i = 0; i < outgoing.Count; i++) outPort[outgoing[i]] = top + RecipeNodeElement.Height * (i + 1) / (outgoing.Count + 1);
                for (var i = 0; i < incoming.Count; i++) inPort[incoming[i]] = top + RecipeNodeElement.Height * (i + 1) / (incoming.Count + 1);
            }

            float FirstHopY(int e)
            {
                var edge = graph.Edges[e];
                return waypoints[e].Count > 0 && !manuallyMoved[edge.FromNode] ? waypoints[e][0].y : nodePositions[edge.ToNode].y;
            }

            float LastHopY(int e)
            {
                var edge = graph.Edges[e];
                return waypoints[e].Count > 0 && !manuallyMoved[edge.ToNode] ? waypoints[e][^1].y : nodePositions[edge.FromNode].y;
            }

            #endregion
        }
    }
}
