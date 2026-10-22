using System.Collections.Generic;
using Client.UitkPoc.RecipeGraph.Model;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.UitkPoc.RecipeGraph.View
{
    // 辺ごとの「資源名 毎分量」ラベルを並べる層
    // Layer holding one "resource rate/min" label per edge
    public class EdgeLabelLayer : VisualElement
    {
        private readonly List<Label> _labels = new();

        public EdgeLabelLayer()
        {
            pickingMode = PickingMode.Ignore;
        }

        public void Rebuild(ProductionGraph graph)
        {
            Clear();
            _labels.Clear();
            foreach (var edge in graph.Edges)
            {
                var label = new Label($"{edge.ResourceName}  {RecipeNodeElement.FormatRate(edge.RatePerMinute)}/分");
                label.AddToClassList("rg-edge-label");
                label.style.borderLeftColor = RecipeEdgeLayer.ColorOf(edge.ResourceName, edge.IsFluid);
                Add(label);
                _labels.Add(label);
            }
        }

        public void Place(Vector2[] anchors)
        {
            EdgeLabelSpreader.Spread(anchors, 120f, 22f);
            for (var e = 0; e < _labels.Count; e++) _labels[e].style.translate = new Translate(anchors[e].x, anchors[e].y);
        }

        public void SetDimmed(HashSet<int> highlightedEdges)
        {
            for (var e = 0; e < _labels.Count; e++) _labels[e].EnableInClassList("rg-edge-label--dim", highlightedEdges != null && !highlightedEdges.Contains(e));
        }
    }
}
