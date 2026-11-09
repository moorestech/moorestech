using UnityEngine;

namespace Client.Ui.Components.NodeGraph
{
    /// <summary>
    /// ズーム・中央寄せ・寸法変化の平行移動。web: moorestech_web/webui/src/shared/treeView/viewport/viewport.ts
    /// Zoom, centering and resize translation; web: moorestech_web/webui/src/shared/treeView/viewport/viewport.ts
    /// </summary>
    internal static class NodeGraphViewportMath
    {
        // Web の MIN_VIEW_SCALE・MAX_VIEW_SCALE・WHEEL_ZOOM_SENSITIVITY
        // The Web MIN_VIEW_SCALE, MAX_VIEW_SCALE and WHEEL_ZOOM_SENSITIVITY
        public const float MinScale = 0.4f;
        public const float MaxScale = 2.5f;
        public const float WheelZoomSensitivity = 0.0015f;

        public static NodeGraphViewport ZoomAt(NodeGraphViewport viewport, Vector2 cursor, float webDeltaY)
        {
            // 倍率を指数で動かし、カーソルの下のワールドの点を固定する
            // Move the scale exponentially and keep the world point under the cursor fixed
            var scale = Mathf.Clamp(viewport.Scale * Mathf.Exp(-webDeltaY * WheelZoomSensitivity), MinScale, MaxScale);
            var worldX = (cursor.x - viewport.X) / viewport.Scale;
            var worldY = (cursor.y - viewport.Y) / viewport.Scale;
            return new NodeGraphViewport(cursor.x - worldX * scale, cursor.y - worldY * scale, scale);
        }

        public static NodeGraphViewport CenterOn(Vector2 canvasPoint, Vector2 viewSize, float scale)
        {
            return new NodeGraphViewport(viewSize.x / 2f - canvasPoint.x * scale, viewSize.y / 2f - canvasPoint.y * scale, scale);
        }

        public static NodeGraphViewport ShiftForResize(NodeGraphViewport current, Vector2 previousSize, Vector2 nextSize)
        {
            // 寸法の差の半分だけずらし、中央に見えていた点を中央に保つ
            // Shift by half the size change so the point seen at the center stays there
            return new NodeGraphViewport(current.X + (nextSize.x - previousSize.x) / 2f, current.Y + (nextSize.y - previousSize.y) / 2f, current.Scale);
        }
    }
}
