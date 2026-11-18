using Client.Ui.Foundation.Text;
using Client.Ui.Foundation.Text.WrapSafe;
using Client.Ui.State.Text;
using UniRx;
using UnityEngine.UIElements;

namespace Client.Ui.Components.Slots
{
    /// <summary>
    /// アイコンの上に白い縁で描く数（Web の .iconTextOutlineLight）。個数と所持/必要の正本
    /// A number drawn over an icon with a white outline (the Web .iconTextOutlineLight); the source of truth for counts and owned/required
    /// </summary>
    /// <remarks>
    /// UITK の outline は字の内外に半分ずつ掛かり細い字を塗りつぶすので、縁だけの Label を後ろに敷き、縁の無い字の Label を上に重ねる 2 層にする。
    /// 2 枚は置く側の要素の兄弟として足し、置き場のクラスを両方に付けて同じ箱にする（入れ子にすると幅の丸めが変わり字の画素がずれる）。
    /// 字の色はつまみ --outlined-count-label-color（既定は DerivedTokens/SlotsDerivedTokens.uss）、大きさ（高さ＝字の大きさ）はつまみ --outlined-count-label-size を置き場のクラスで渡す。訳さない数字は UiText.FromVerbatim で渡す。
    /// A UITK outline straddles the glyph edge and floods thin glyphs, so a stroke-only Label sits behind an unoutlined glyph Label in two layers.
    /// Both are added as siblings into the placing element and both get the placement class so they share one box (nesting changes width rounding and shifts glyph pixels).
    /// The glyph color comes through the --outlined-count-label-color knob (default in DerivedTokens/SlotsDerivedTokens.uss) and the size (height = font size) through --outlined-count-label-size set by the placement class; untranslated digits come in as UiText.FromVerbatim.
    /// </remarks>
    public sealed class OutlinedCountLabel
    {
        private readonly Label _stroke;
        private readonly Label _glyph;
        private LocalizedTextBinding _binding;

        // layerClasses は書体など、置き場のほかに 2 枚へそろえて当てるクラス（無ければ渡さない）
        // layerClasses are classes besides the placement, such as typography, applied to both layers alike (pass none if there are none)
        public OutlinedCountLabel(VisualElement parent, string placementClass, params string[] layerClasses)
        {
            // 縁の層を先に足し、字を上に描く
            // The stroke layer goes first so the glyph draws on top
            _stroke = CreateLayer("outlined-count-label--stroke");
            _glyph = CreateLayer("outlined-count-label--glyph");

            #region Internal

            Label CreateLayer(string layerClass)
            {
                var layer = new WrapSafeLabel { pickingMode = PickingMode.Ignore };
                layer.AddToClassList("outlined-count-label");
                layer.AddToClassList(layerClass);
                layer.AddToClassList(placementClass);
                foreach (var extraClass in layerClasses) layer.AddToClassList(extraClass);
                parent.Add(layer);
                return layer;
            }

            #endregion
        }

        public void SetText(UiText text)
        {
            // 文の無い束縛をパネルに載せないよう初回に作る（載って文が無いと LocalizedTextBinding が LogError を出す）
            // Create the binding on first use so no text-less binding sits on the panel (LocalizedTextBinding logs an error for that)
            if (_binding == null)
            {
                _binding = new LocalizedTextBinding(_glyph);
                _binding.OnResolved.Subscribe(resolved =>
                {
                    _stroke.text = resolved;
                    _glyph.text = resolved;
                });
            }
            _binding.SetText(text);
        }
    }
}
