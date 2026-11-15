using Client.Ui.Foundation.Input;
using Client.Ui.Foundation.Layout;
using Client.Ui.Screens.Tutorial.Pulse;
using Client.Ui.State.Tutorial.Logic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.Tutorial.WorldPin
{
    /// <summary>
    /// 画面外のピンを指す画面端の矢印 1 つ（Web EdgeArrow）
    /// One screen-edge arrow pointing at an off-screen pin (Web EdgeArrow)
    /// </summary>
    public sealed class WorldPinEdgeArrowView : VisualElement
    {
        private readonly VisualElement _glyph = new();
        private readonly TutorialPulseBinding _pulse;
        private Color _fill;
        private Color _stroke;
        private Color _shadow;
        private bool _hasColors;

        public WorldPinEdgeArrowView(TutorialPulseBinding pulse)
        {
            _pulse = pulse;
            AddToClassList("world-pin__arrow");
            _glyph.AddToClassList("world-pin__arrow-glyph");
            _glyph.generateVisualContent += DrawArrow;
            Add(_glyph);
            UiPickingIgnore.ApplyToSubtree(this);
            // 位置と回転は親のインライン、脈動は子の矢印本体だけ（ADR 0039）
            // Position and rotation live inline on the parent; only the child glyph pulses (ADR 0039)
            pulse.Attach(_glyph, TutorialAttentionPulse.StrongScale);
        }

        public void Show(string pinId, WorldPinEdgeSpot spot, float size)
        {
            // 中心を端のクランプ位置へ合わせ、ピンの方向へ回す（Web の translate(-50%, -50%) rotate）
            // Center on the clamped edge spot and rotate toward the pin (Web translate(-50%, -50%) rotate)
            name = $"world-pin-arrow-{pinId}";
            style.width = size;
            style.height = size;
            style.left = spot.Left;
            style.top = spot.Top;
            style.translate = new Translate(Length.Percent(-50), Length.Percent(-50));
            style.rotate = new Rotate(Angle.Degrees(spot.AngleDegrees));
            UiDisplay.SetShown(this, true);
        }

        public void HideArrow()
        {
            UiDisplay.SetShown(this, false);
        }

        public void Discard()
        {
            // 木から外し、矢印本体を脈動の管理辞書から外す（Attach のコールバックは要素に残るが Start は管理外を流さない）
            // Remove from the tree and drop the glyph from the pulse dictionary (Attach callbacks stay on the element but Start never pulses an untracked target)
            RemoveFromHierarchy();
            _pulse.Detach(_glyph);
        }

        public void SetColors(Color fill, Color stroke, Color shadow)
        {
            _fill = fill;
            _stroke = stroke;
            _shadow = shadow;
            _hasColors = true;
            _glyph.MarkDirtyRepaint();
        }

        private void DrawArrow(MeshGenerationContext context)
        {
            // silent-ok: 色の解決前は描かない / 解決できなければ UssCustomStyleReader が LogError を出す
            if (!_hasColors) return;
            WorldPinGlyphs.DrawArrow(context.painter2D, _glyph.contentRect.width, _fill, _stroke, _shadow);
        }
    }
}
