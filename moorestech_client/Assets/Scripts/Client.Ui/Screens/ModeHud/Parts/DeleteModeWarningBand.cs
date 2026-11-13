using Client.Ui.Foundation.Theme;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.ModeHud.Parts
{
    /// <summary>
    /// 削除モードの警告帯 1 本（黄の斜線は Painter2D）
    /// One delete-mode warning band (yellow stripes via Painter2D)
    /// </summary>
    public sealed class DeleteModeWarningBand : VisualElement
    {
        // :root の変数は customStyle に継承されないので、帯のクラスで受け直した名前を読む
        // :root variables do not inherit into customStyle, so read the names re-declared on the band class
        private const string YellowProperty = "--mode-hud-band-yellow";
        private const string StripeSizeProperty = "--mode-hud-band-stripe-size";
        private const string StripeAngleProperty = "--mode-hud-band-stripe-angle";

        private Color _yellow;
        private float _stripeSize;
        private float _angleDegrees;
        private bool _resolved;

        public DeleteModeWarningBand(string testid, string edgeClassName)
        {
            name = testid;
            pickingMode = PickingMode.Ignore;
            AddToClassList("mode-hud__warning-band");
            AddToClassList(edgeClassName);
            RegisterCallback<CustomStyleResolvedEvent>(OnCustomStyleResolved);
            RegisterCallback<GeometryChangedEvent>(_ => MarkDirtyRepaint());
            generateVisualContent += DrawStripes;
        }

        private void OnCustomStyleResolved(CustomStyleResolvedEvent evt)
        {
            // 3 つ揃った時だけ描く
            // Draw only when all three resolve
            var hasYellow = UssCustomStyleReader.TryReadColor(evt.customStyle, YellowProperty, out _yellow);
            var hasSize = UssCustomStyleReader.TryReadPixels(evt.customStyle, StripeSizeProperty, out _stripeSize);
            var hasAngle = UssCustomStyleReader.TryReadDegrees(evt.customStyle, StripeAngleProperty, out _angleDegrees);
            // 刻み幅 0 以下は帯を作れない（周期のループが進まない）。黙らず理由を出して描かない
            // A stripe size of 0 or less cannot make stripes (the period loop never advances); log why and skip drawing
            if (hasSize && _stripeSize <= 0f) Debug.LogError($"[DeleteModeWarningBand] {StripeSizeProperty} must be positive but was {_stripeSize}");
            _resolved = hasYellow && hasSize && hasAngle && 0f < _stripeSize;
            MarkDirtyRepaint();
        }

        private void DrawStripes(MeshGenerationContext context)
        {
            // silent-ok: 解決前と寸法 0 は描かない / 読めないつまみは OnCustomStyleResolved の読み手が LogError で知らせる
            if (!_resolved || contentRect.width <= 0f || contentRect.height <= 0f) return;

            // 黄の帯を 1 枚ずつ塗る
            // Fill each yellow stripe
            var painter = context.painter2D;
            painter.fillColor = _yellow;
            foreach (var polygon in DeleteModeWarningStripeGeometry.FirstColorPolygons(contentRect.size, _angleDegrees, _stripeSize))
            {
                painter.BeginPath();
                painter.MoveTo(polygon[0]);
                for (var i = 1; i < polygon.Count; i++) painter.LineTo(polygon[i]);
                painter.ClosePath();
                painter.Fill();
            }
        }
    }
}
