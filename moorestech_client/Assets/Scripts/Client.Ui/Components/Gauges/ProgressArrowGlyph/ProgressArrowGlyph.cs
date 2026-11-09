using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Ui.Components.Gauges
{
    /// <summary>
    /// 矢印形の進捗ゲージ: 溝・充填・輪郭 3 層 + 影
    /// Arrow progress gauge: track, fill, outline layers plus shadow
    /// </summary>
    /// <remarks>
    /// 作った直後は静止表示（Web の value=null。充填層を持たない）。SetProgress(0) は幅 0 の充填を持つ進捗で静止とは別。
    /// 寸法は --craft-arrow-width/height を部品だけが持ち、viewBox 121×78 を SVG 既定の xMidYMid meet で当てはめる。
    /// Starts in the static form (Web value=null, no fill layer); SetProgress(0) is progress with a zero-width fill, distinct from static.
    /// Only the component holds the --craft-arrow-width/height size and fits the 121x78 viewBox with the SVG default xMidYMid meet.
    /// </remarks>
    public sealed class ProgressArrowGlyph : VisualElement
    {
        private const string IdleClassName = "progress-arrow-glyph--idle";

        private readonly VisualElement _viewBox;
        private readonly VisualElement _fillClip;
        private readonly ProgressArrowGlyphFace _fill;

        public ProgressArrowGlyph(string testid)
        {
            name = testid;
            pickingMode = PickingMode.Ignore;
            AddToClassList("progress-arrow-glyph");

            // SVG と同順: 影→溝→充填→輪郭
            // Same order as the SVG: shadow, track, fill, outline
            _viewBox = NewPart("progress-arrow-glyph__view-box");
            Add(_viewBox);
            _viewBox.Add(new ProgressArrowGlyphShadow());
            _viewBox.Add(new ProgressArrowGlyphFace("progress-arrow-glyph__track"));
            _fillClip = NewPart("progress-arrow-glyph__fill-clip");
            _fillClip.style.left = Length.Percent(ProgressArrowGlyphPath.ArrowLeft / ProgressArrowGlyphPath.ViewBoxWidth * 100f);
            _viewBox.Add(_fillClip);
            _fill = new ProgressArrowGlyphFace("progress-arrow-glyph__fill");
            _fillClip.Add(_fill);
            _viewBox.Add(new ProgressArrowGlyphOutline());

            RegisterCallback<GeometryChangedEvent>(_ => FitViewBox());
            SetIdle();
        }

        public void SetProgress(float ratio01)
        {
            // Web の clipPath の rect（x=2・幅 117×値）を、viewBox の幅に対する % の切り抜きで写す
            // Copy the Web clipPath rect (x=2, width 117 times value) as a clip in percent of the viewBox width
            var clamped = GaugeRatio.Clamp01(ratio01);
            _fillClip.style.width = Length.Percent(ProgressArrowGlyphPath.ArrowSpan * clamped / ProgressArrowGlyphPath.ViewBoxWidth * 100f);
            RemoveFromClassList(IdleClassName);
        }

        public void SetIdle()
        {
            AddToClassList(IdleClassName);
        }

        private void FitViewBox()
        {
            // NaN・負の寸法はトークン誤りで通知
            // NaN or negative size is a token mistake; report
            var size = contentRect.size;
            if (float.IsNaN(size.x) || float.IsNaN(size.y) || size.x < 0f || size.y < 0f)
            {
                Debug.LogError($"[ProgressArrowGlyph] {name} has an invalid size {size}; the arrow is not fitted");
                return;
            }

            // silent-ok: 非表示の祖先の下と配置前は寸法 0 なので当てはめない / 寸法のつまみの誤りは L1 の寸法テストと並べ比べが捕まえる（前例 DeleteModeWarningBand）
            // silent-ok: a hidden ancestor or pre-layout gives size 0, so skip fitting / L1 size tests and the comparison catch wrong size knobs (precedent DeleteModeWarningBand)
            if (size.x <= 0f || size.y <= 0f) return;
            var box = ProgressArrowGlyphPath.FitViewBox(size);

            // meet 位置に箱を置き充填面を原点へ揃える
            // Place box at meet position; align fill face to origin
            _viewBox.style.left = box.x;
            _viewBox.style.top = box.y;
            _viewBox.style.width = box.width;
            _viewBox.style.height = box.height;
            _fill.style.left = -ProgressArrowGlyphPath.ArrowLeft * box.width / ProgressArrowGlyphPath.ViewBoxWidth;
            _fill.style.width = box.width;
        }

        private static VisualElement NewPart(string className)
        {
            var part = new VisualElement { pickingMode = PickingMode.Ignore };
            part.AddToClassList(className);
            return part;
        }
    }
}
