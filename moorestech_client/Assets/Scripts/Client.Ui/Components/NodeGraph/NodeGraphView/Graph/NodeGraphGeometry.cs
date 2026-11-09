using System.Collections.Generic;
using UnityEngine;

namespace Client.Ui.Components.NodeGraph
{
    /// <summary>
    /// ツリー座標（Y 上向き）とキャンバス座標（Y 下向き）の写像。web: moorestech_web/webui/src/shared/treeView/treeGeometry.ts
    /// Mapping between tree coordinates (Y up) and canvas coordinates (Y down); web: moorestech_web/webui/src/shared/treeView/treeGeometry.ts
    /// </summary>
    internal static class NodeGraphGeometry
    {
        // Web の TreeView.tsx が境界に渡す余白（computeTreeCanvasBounds(..., 200)）
        // The padding the Web TreeView.tsx passes to the bounds (computeTreeCanvasBounds(..., 200))
        public const float CanvasPadding = 200f;

        public static NodeGraphCanvasBounds ComputeCanvasBounds(IReadOnlyList<Vector2> treePositions, float padding)
        {
            // ノードが無ければ余白だけの正方形
            // With no nodes the canvas is the padding square alone
            if (treePositions.Count == 0) return new NodeGraphCanvasBounds(padding * 2f, padding * 2f, padding, padding);

            var min = treePositions[0];
            var max = treePositions[0];
            foreach (var position in treePositions)
            {
                min = Vector2.Min(min, position);
                max = Vector2.Max(max, position);
            }

            // 上端は Y の最大（Y を下向きへ裏返す）
            // The top edge is the largest Y (Y is flipped downward)
            return new NodeGraphCanvasBounds(max.x - min.x + padding * 2f, max.y - min.y + padding * 2f, padding - min.x, max.y + padding);
        }

        public static Vector2 ToCanvasPoint(Vector2 treePoint, NodeGraphCanvasBounds bounds)
        {
            return new Vector2(treePoint.x + bounds.OffsetX, bounds.OffsetY - treePoint.y);
        }

        public static NodeGraphLine LineBetween(Vector2 from, Vector2 to)
        {
            var delta = to - from;
            return new NodeGraphLine(from, delta.magnitude, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
        }
    }
}
