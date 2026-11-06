using Client.Ui.Foundation.Theme;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Ui.Components.Decor
{
    /// <summary>
    /// 両端を透明へ落とす横グラデの罫線要素。停止位置は Web の 0%・5%・11%・89%・95%・100%（FadeRule の基底で、GamePanel の題の罫線も直に使う）
    /// Horizontal rule element fading both ends; stops are the Web 0/5/11/89/95/100% (base of FadeRule, also used directly for the GamePanel title rules)
    /// </summary>
    /// <remarks>
    /// USS に linear-gradient が無いため Painter2D で塗る。色を受け直す 3 つの変数名は使う側が渡し、3 色は同じ RGB で α だけ違うので RGB は中央の色から取る
    /// USS has no linear-gradient, so Painter2D paints it; callers pass the three re-declared variable names, and since the colors share one RGB the RGB comes from the middle color
    /// </remarks>
    public class FadeGradientRule : VisualElement
    {
        private readonly string _startProperty;
        private readonly string _peakProperty;
        private readonly string _endProperty;
        private readonly Gradient _gradient = new();
        private bool _hasColors;

        public FadeGradientRule(string startProperty, string peakProperty, string endProperty)
        {
            _startProperty = startProperty;
            _peakProperty = peakProperty;
            _endProperty = endProperty;
            pickingMode = PickingMode.Ignore;
            generateVisualContent += DrawRule;
            RegisterCallback<CustomStyleResolvedEvent>(OnCustomStyleResolved);
        }

        private void OnCustomStyleResolved(CustomStyleResolvedEvent evt)
        {
            // 読めなくなったときも描き直し、前回のグラデを残さない
            // Repaint even when unreadable so the previous gradient never lingers
            _hasColors = TryBuildGradient();
            MarkDirtyRepaint();
        }

        private bool TryBuildGradient()
        {
            // 3 色とも読めたときだけ組む。読めない名前は UssCustomStyleReader が LogError で出す
            // Build only when all three resolve; UssCustomStyleReader logs any unresolved name
            var hasStart = UssCustomStyleReader.TryReadColor(customStyle, _startProperty, out var start);
            var hasPeak = UssCustomStyleReader.TryReadColor(customStyle, _peakProperty, out var peak);
            var hasEnd = UssCustomStyleReader.TryReadColor(customStyle, _endProperty, out var end);
            if (!(hasStart && hasPeak && hasEnd)) return false;

            var opaquePeak = new Color(peak.r, peak.g, peak.b, 1f);
            _gradient.SetKeys(
                new[] { new GradientColorKey(opaquePeak, 0f), new GradientColorKey(opaquePeak, 1f) },
                new[]
                {
                    new GradientAlphaKey(0f, 0f), new GradientAlphaKey(start.a, 0.05f), new GradientAlphaKey(peak.a, 0.11f),
                    new GradientAlphaKey(peak.a, 0.89f), new GradientAlphaKey(end.a, 0.95f), new GradientAlphaKey(0f, 1f),
                });
            return true;
        }

        private void DrawRule(MeshGenerationContext context)
        {
            // silent-ok: 色の解決前は描かない / 解決できなければ UssCustomStyleReader が LogError を出す
            if (!_hasColors) return;

            // 要素の矩形いっぱいを左から右への横グラデで塗る
            // Fill the element rect with a left-to-right gradient
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
