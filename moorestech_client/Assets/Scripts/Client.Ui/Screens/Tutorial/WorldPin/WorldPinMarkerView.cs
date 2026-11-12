using Client.Ui.Foundation.Input;
using Client.Ui.Foundation.Text;
using Client.Ui.State.Tutorial.WorldPin;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.Tutorial.WorldPin
{
    /// <summary>
    /// 画面内のピン 1 つ。ラベルとマーカーを縦に積み、下端中央を射影座標に置く（Web OnScreenPin）
    /// One on-screen pin; stacks the label and marker and puts its bottom center on the projected point (Web OnScreenPin)
    /// </summary>
    public sealed class WorldPinMarkerView : VisualElement
    {
        private readonly LocalizedLabel _label = new();
        private readonly VisualElement _marker = new();
        private readonly VisualElement _markerGlyph = new();
        private Color _face;
        private Color _stroke;
        private bool _hasColors;

        public WorldPinMarkerView()
        {
            AddToClassList("world-pin");
            _label.AddToClassList("world-pin__label");
            _marker.AddToClassList("world-pin__marker");
            // 描くのは子。Web の svg は viewBox の上へはみ出た頂を切るので、20×20 の箱（overflow: hidden）の中で描いて同じく切る（前例 WorldPinEdgeArrowView の _glyph）
            // The child draws; the Web svg clips the apex above its viewBox, so draw inside the 20x20 box (overflow: hidden) to clip it the same (precedent WorldPinEdgeArrowView._glyph)
            _markerGlyph.AddToClassList("world-pin__marker-glyph");
            _markerGlyph.generateVisualContent += DrawMarker;
            _marker.Add(_markerGlyph);
            Add(_label);
            Add(_marker);
            UiPickingIgnore.ApplyToSubtree(this);
        }

        public void Show(WorldPinCue.OnScreen cue)
        {
            // 位置は層に対する割合（Web の left/top %）、下端中央への吊り下げは USS の translate
            // Position as a fraction of the layer (Web left/top %); the bottom-center hang is the USS translate
            name = $"world-pin-{cue.PinId}";
            style.left = Length.Percent(cue.ScreenX * 100f);
            style.top = Length.Percent(cue.ScreenY * 100f);
            _label.SetText(cue.Label);
        }

        public void SetColors(Color face, Color stroke)
        {
            _face = face;
            _stroke = stroke;
            _hasColors = true;
            _markerGlyph.MarkDirtyRepaint();
        }

        private void DrawMarker(MeshGenerationContext context)
        {
            // silent-ok: 色の解決前は描かない / 解決できなければ UssCustomStyleReader が LogError を出す
            if (!_hasColors) return;
            WorldPinGlyphs.DrawMarker(context.painter2D, _markerGlyph.contentRect.width, _face, _stroke);
        }
    }
}
