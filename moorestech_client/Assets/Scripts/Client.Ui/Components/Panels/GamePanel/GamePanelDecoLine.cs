using Client.Ui.Foundation.Theme;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Ui.Components.Panels
{
    /// <summary>
    /// タイトル行の上下の罫線
    /// Rules above and below the title row
    /// </summary>
    /// <remarks>
    /// USSにgradientが無いので描画で塗る
    /// USS has no gradient, so paint it
    /// </remarks>
    public class GamePanelDecoLine : VisualElement
    {
        private readonly Gradient _gradient = new();
        private bool _hasColors;

        public GamePanelDecoLine(string positionClassName)
        {
            AddToClassList("game-panel__deco-line");
            AddToClassList(positionClassName);
            pickingMode = PickingMode.Ignore;
            generateVisualContent += DrawLine;
            RegisterCallback<CustomStyleResolvedEvent>(OnCustomStyleResolved);
        }

        private void OnCustomStyleResolved(CustomStyleResolvedEvent evt)
        {
            // RGBは中央の色から取る
            // Take the RGB from the middle color
            var hasStart = UssCustomStyleReader.TryReadColor(customStyle, "--game-panel-rule-start", out var start);
            var hasPeak = UssCustomStyleReader.TryReadColor(customStyle, "--game-panel-rule-peak", out var peak);
            var hasEnd = UssCustomStyleReader.TryReadColor(customStyle, "--game-panel-rule-end", out var end);
            _hasColors = hasStart && hasPeak && hasEnd;

            // 読めなくても描き直す
            // Repaint even when unreadable
            MarkDirtyRepaint();
            if (!_hasColors) return;

            var opaquePeak = new Color(peak.r, peak.g, peak.b, 1f);
            _gradient.SetKeys(
                new[] { new GradientColorKey(opaquePeak, 0f), new GradientColorKey(opaquePeak, 1f) },
                new[]
                {
                    new GradientAlphaKey(0f, 0f), new GradientAlphaKey(start.a, 0.05f), new GradientAlphaKey(peak.a, 0.11f),
                    new GradientAlphaKey(peak.a, 0.89f), new GradientAlphaKey(end.a, 0.95f), new GradientAlphaKey(0f, 1f),
                });
        }

        private void DrawLine(MeshGenerationContext context)
        {
            // silent-ok: 色の解決前は描かない / 解決できなければ UssCustomStyleReader が LogError を出す
            if (!_hasColors) return;

            var size = contentRect.size;
            var painter = context.painter2D;
            painter.fillColor = Color.white;
            painter.fillGradient = FillGradient.MakeLinearGradient(_gradient, Vector2.zero, new Vector2(size.x, 0f), AddressMode.Clamp);
            painter.BeginPath();
            painter.MoveTo(Vector2.zero);
            painter.LineTo(new Vector2(size.x, 0f));
            painter.LineTo(size);
            painter.LineTo(new Vector2(0f, size.y));
            painter.ClosePath();
            painter.Fill();
        }
    }
}
