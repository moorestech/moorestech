using Client.Ui.Foundation.Theme;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.Recipe.Parts
{
    /// <summary>
    /// 見出しの装飾線（Web ItemHeader の linear-gradient を頂点カラーで写し、中央の菱形を子に持つ）
    /// Header decoration line (vertex-color port of the Web ItemHeader linear-gradient, with the center diamond as a child)
    /// </summary>
    public sealed class ItemHeaderRule : VisualElement
    {
        private const string RuleColorProperty = "--item-header-rule-color";

        // Web の色止め（0 透明・20%・44%・48% 透明・52% 透明・56%・80%・100% 透明）
        // Web color stops (0 clear, 20%, 44%, 48% clear, 52% clear, 56%, 80%, 100% clear)
        private static readonly float[] Stops = { 0f, 0.2f, 0.44f, 0.48f, 0.52f, 0.56f, 0.8f, 1f };
        private static readonly bool[] Opaque = { false, true, true, false, false, true, true, false };
        private Color _ruleColor;
        private bool _hasColor;

        public ItemHeaderRule()
        {
            AddToClassList("item-header__rule");
            pickingMode = PickingMode.Ignore;
            var diamond = new VisualElement { pickingMode = PickingMode.Ignore };
            diamond.AddToClassList("item-header__diamond");
            Add(diamond);
            RegisterCallback<CustomStyleResolvedEvent>(_ => OnStyleResolved());
            generateVisualContent += Draw;
        }

        private void OnStyleResolved()
        {
            // 読めないときは UssCustomStyleReader が LogError を出し、線を描かない
            // When unreadable UssCustomStyleReader logs an error and the line is not drawn
            _hasColor = UssCustomStyleReader.TryReadColor(customStyle, RuleColorProperty, out _ruleColor);
            MarkDirtyRepaint();
        }

        private void Draw(MeshGenerationContext context)
        {
            // silent-ok: 色が読めない間は描かない / 読めなかった名前は UssCustomStyleReader が LogError で出す
            if (!_hasColor) return;
            var rect = contentRect;
            // silent-ok: 幅 0 はレイアウト前の一瞬だけ / 寸法が決まると UITK が描き直す
            if (rect.width <= 0f) return;

            // 高さの中央に 1px の帯を色止めごとの上下 2 頂点で張る（Web center / 100% 1px）
            // Lay a 1px band at mid-height with a top and bottom vertex per color stop (Web center / 100% 1px)
            var top = rect.height * 0.5f - 0.5f;
            var clear = new Color(_ruleColor.r, _ruleColor.g, _ruleColor.b, 0f);
            var mesh = context.Allocate(Stops.Length * 2, (Stops.Length - 1) * 6);
            for (var i = 0; i < Stops.Length; i++)
            {
                var tint = Opaque[i] ? _ruleColor : clear;
                var x = rect.width * Stops[i];
                mesh.SetNextVertex(new Vertex { position = new Vector3(x, top, Vertex.nearZ), tint = tint });
                mesh.SetNextVertex(new Vertex { position = new Vector3(x, top + 1f, Vertex.nearZ), tint = tint });
            }

            // 画面座標（y 下向き）で時計回りに 2 枚ずつ張る（前例 CrosshairShadowRing）
            // Wind two triangles per span clockwise in screen space (y down; precedent CrosshairShadowRing)
            for (var i = 0; i < Stops.Length - 1; i++)
            {
                var a = (ushort)(i * 2);
                mesh.SetNextIndex(a);
                mesh.SetNextIndex((ushort)(a + 2));
                mesh.SetNextIndex((ushort)(a + 1));
                mesh.SetNextIndex((ushort)(a + 1));
                mesh.SetNextIndex((ushort)(a + 2));
                mesh.SetNextIndex((ushort)(a + 3));
            }
        }
    }
}
