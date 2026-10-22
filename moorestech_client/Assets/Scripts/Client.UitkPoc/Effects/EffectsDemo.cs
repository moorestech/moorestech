using Client.UitkPoc.Shell;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.UitkPoc.Effects
{
    // CSSなら1行の見た目表現を、USSで素直に書けるか・工夫が要るか・できないかを並べるカタログ
    // Catalog of one-line CSS looks: whether USS does them natively, needs a workaround, or cannot
    public class EffectsDemo : IUitkPocDemo
    {
        private readonly UitkPocContext _context;

        public string Title => "表現";

        public EffectsDemo(UitkPocContext context)
        {
            _context = context;
        }

        public VisualElement CreateView()
        {
            var root = new ScrollView();
            root.styleSheets.Add(Resources.Load<StyleSheet>("UitkPoc/Effects"));
            root.AddToClassList("fx-root");
            root.contentContainer.AddToClassList("fx-grid");
            var icon = _context.LoadItemIcon("ICチップ");

            // 背景・形
            // Backgrounds and shapes
            var gradient = Card("linear-gradient() / radial-gradient()", EffectVerdict.Workaround, "USSの背景には書けない。左: 256x1テクスチャを焼いて背景に / 右: 6.3のPainter2D FillGradientで放射状に塗る");
            gradient.Sample.Add(Box("fx-gradient-box", GeneratedTextures.LinearGradient(new Color(0.95f, 0.45f, 0.2f), new Color(0.45f, 0.2f, 0.9f), true)));
            gradient.Sample.Add(new RadialPanelElement(new Color(0.3f, 0.8f, 1f), new Color(0.05f, 0.1f, 0.3f)) { name = "radial" });
            var hexagon = Card("clip-path: polygon() + グラデ", EffectVerdict.Workaround, "Painter2Dでパスを塗る。fillGradientで塗り分け可。中身の子要素は多角形で切り抜けない");
            var hexGradient = new Gradient();
            hexGradient.SetKeys(new[] { new GradientColorKey(new Color(0.2f, 0.8f, 0.6f), 0f), new GradientColorKey(new Color(0.1f, 0.3f, 0.8f), 1f) }, new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            hexagon.Sample.Add(new HexagonElement(hexGradient) { name = "hexagon" });
            var glow = Card("radial-gradient() のグロー", EffectVerdict.Workaround, "放射状アルファのテクスチャを焼いて重ねる");
            var glowHost = Box("fx-glow", GeneratedTextures.RadialGlow(new Color(1f, 0.7f, 0.2f, 0.9f), 128));
            glowHost.Add(Box("fx-icon", icon));
            glow.Sample.Add(glowHost);
            var shadow = Card("box-shadow（9スライス）", EffectVerdict.Workaround, "ぼけた角丸影を焼いて9スライスで伸ばす。任意サイズで崩れない");
            var shadowHost = new VisualElement();
            shadowHost.AddToClassList("fx-shadow-host");
            shadowHost.Add(Box("fx-shadow", GeneratedTextures.SoftShadow(96, 14f, 18f, new Color(0, 0, 0, 0.75f))));
            shadowHost.Add(Label("fx-floating-card", "浮いたカード"));
            shadow.Sample.Add(shadowHost);
            var filterShadow = Card("box-shadow（filter: blur）", EffectVerdict.Native, "6.3の新機能filterで、背面の黒い板をぼかして影にする");
            var filterShadowHost = new VisualElement();
            filterShadowHost.AddToClassList("fx-shadow-host");
            filterShadowHost.Add(Label("fx-blur-shadow", ""));
            filterShadowHost.Add(Label("fx-floating-card", "浮いたカード"));
            filterShadow.Sample.Add(filterShadowHost);

            // フィルタ
            // Filters
            var filters = Card("filter: grayscale / hue-rotate / invert / sepia / blur", EffectVerdict.Native, "6.3からUSSのfilterで書ける（無効アイテムの灰色化などに使える）");
            foreach (var modifier in new[] { "none", "grayscale", "hue", "invert", "sepia", "blur" }) filters.Sample.Add(Box("fx-icon fx-filter--" + modifier, icon));
            var backdrop = Card("backdrop-filter: blur()（すりガラス）", EffectVerdict.Impossible, "filterは自分と子をぼかすだけで、背後は見えない。背面をRenderTextureへ描いて敷く等の自作が要る");
            var backdropHost = new VisualElement();
            backdropHost.AddToClassList("fx-backdrop-host");
            for (var i = 0; i < 5; i++) backdropHost.Add(Box("fx-icon", _context.LoadItemIcon(i % 2 == 0 ? "ICチップ" : "ガラス")));
            backdropHost.Add(Label("fx-glass", "すりガラスのつもり"));
            backdrop.Sample.Add(backdropHost);

            // 線・文字
            // Lines and text
            var dashed = Card("border-style: dashed", EffectVerdict.Workaround, "枠線は実線のみ。Painter2DのSetDashPatternで描く");
            dashed.Sample.Add(new DashedBorderElement { name = "dashed" });
            var ring = Card("conic-gradient() の進捗リング", EffectVerdict.Workaround, "Painter2D.Arc＋strokeGradient。毎フレーム再描画");
            var progressRing = new ProgressRingElement { name = "ring" };
            progressRing.schedule.Execute(timer => progressRing.SetProgress(Time.realtimeSinceStartup * 0.25f)).Every(16);
            ring.Sample.Add(progressRing);
            var textEffects = Card("text-shadow / 文字の縁取り", EffectVerdict.Native, "text-shadowと-unity-text-outlineは素で書ける。グラデーション文字は不可");
            textEffects.Sample.Add(Label("fx-glow-text", "MOORESTECH"));
            textEffects.Sample.Add(Label("fx-outline-text", "縁取り文字"));
            var pseudo = Card(":nth-child / :last-child", EffectVerdict.Impossible, "未対応（警告が出て無視される）。縞模様や末尾の余白はコードでクラスを付ける");
            for (var i = 0; i < 4; i++) pseudo.Sample.Add(Label(i % 2 == 0 ? "fx-stripe fx-stripe--even" : "fx-stripe", $"行 {i + 1}（コードで付けた縞）"));
            var layering = Card("z-index / gap / grid", EffectVerdict.Workaround, "z-indexは無く兄弟順で決まる（BringToFront）。gapとgridも無く、このカード並びはflex-wrap＋margin");
            var stack = new VisualElement();
            stack.AddToClassList("fx-stack");
            for (var i = 0; i < 3; i++)
            {
                var layer = Label("fx-stack__card fx-stack__card--" + i, $"カード{i + 1}（クリックで前面へ）");
                layer.RegisterCallback<PointerDownEvent>(_ => layer.BringToFront());
                stack.Add(layer);
            }
            layering.Sample.Add(stack);
            return root;

            #region Internal

            EffectCard Card(string cssName, EffectVerdict verdict, string note)
            {
                var card = new EffectCard(cssName, verdict, note);
                root.Add(card);
                return card;
            }

            VisualElement Box(string classes, Texture2D background)
            {
                var box = new VisualElement();
                foreach (var cls in classes.Split(' ')) box.AddToClassList(cls);
                box.style.backgroundImage = background;
                return box;
            }

            Label Label(string classes, string text)
            {
                var label = new UnityEngine.UIElements.Label(text);
                foreach (var cls in classes.Split(' ')) label.AddToClassList(cls);
                return label;
            }

            #endregion
        }
    }
}
