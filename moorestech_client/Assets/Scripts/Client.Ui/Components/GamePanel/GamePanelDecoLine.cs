using Client.Ui.Foundation.Theme;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Ui.Components
{
    /// <summary>
    /// タイトル行の上下の罫線。中央を保ち両端を透明へ落とす横グラデーションを Painter2D で塗る（Web の .decoLine）
    /// Title-row rule above or below; paints a horizontal gradient that keeps the middle and fades both ends with Painter2D (the Web .decoLine)
    /// </summary>
    /// <remarks>
    /// USS に linear-gradient が無いため描画で塗る。停止位置は Web の 0%・5%・11%・89%・95%・100%
    /// USS has no linear-gradient, so it is painted; the stops are the Web 0%, 5%, 11%, 89%, 95%, 100%
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
            // 色は 3 つとも同じ RGB で α だけ違う。RGB は中央の色から取る
            // All three share one RGB and differ only in alpha; take the RGB from the middle color
            var hasStart = UssCustomStyleReader.TryReadColor(customStyle, "--game-panel-rule-start", out var start);
            var hasPeak = UssCustomStyleReader.TryReadColor(customStyle, "--game-panel-rule-peak", out var peak);
            var hasEnd = UssCustomStyleReader.TryReadColor(customStyle, "--game-panel-rule-end", out var end);
            _hasColors = hasStart && hasPeak && hasEnd;

            // 読めなくなったときも描き直し、前回のグラデーションを残さない（BottomDeco・Face と同じ扱い）
            // Repaint even when unreadable so the previous gradient never lingers (same as BottomDeco and Face)
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
