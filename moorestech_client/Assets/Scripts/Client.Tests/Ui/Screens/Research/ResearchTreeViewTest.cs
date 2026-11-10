using System;
using Client.Tests.Ui.TestDoubles;
using Client.Ui.Content;
using Client.Ui.Foundation.Input.OsCursor;
using Client.Ui.Foundation.Layers;
using Client.Ui.Foundation.Stage;
using Client.Ui.Screens.Progress;
using Client.Ui.Screens.Research;
using Client.Ui.State.Research.Tree;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;
using Kit = Client.Tests.Ui.Screens.Research.ResearchScreenTestKit;

namespace Client.Tests.Ui.Screens.Research
{
    /// <summary>
    /// 研究エリアの描画・選択・層順（L1）
    /// Research area draw, selection, layers (L1)
    /// </summary>
    public class ResearchTreeViewTest : ScreenTestHost
    {
        // Webの持ち物幅と列間隔
        // Web inventory width and column gap
        private const float InventoryPanelWidth = 473.891247f;
        private const float StageColumnGap = 16f;

        private Texture2D _texture;
        private GameContentPresentation _content;
        private RecordingResearchTreeCommands _commands;
        private UiLayerStack _layers;
        private VisualElement _layer;
        private ResearchTreeView _view;

        [SetUp]
        public void SetUp()
        {
            _texture = new Texture2D(4, 4) { hideFlags = HideFlags.DontSave };
            _content = Kit.LoadMasterAndContent(_texture);
            _commands = new RecordingResearchTreeCommands();

            // 本番と同じ層へ載せる
            // Mount on the production layer
            _view = new ResearchTreeView(_commands, _content, new UiOsCursorRequestStack(new FakeUiOsCursorApplier()));
            _view.Render(Kit.Tree(ResearchSelection.None));
            _view.SetShown(true);
            _layers = UiLayerStack.Build(rootVisualElement);
            _layer = _layers.GetLayer(UiLayer.OverlayPanel, UiFamily.Stage);
            _layer.Add(_view);
            simulate.FrameUpdate();
            simulate.FrameUpdate();
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_texture);
        }

        /// <web-test file="e2e/tests/research/research.spec.ts">research tree renders nodes when uiState enters ResearchTree</web-test>
        [Test]
        public void 研究画面を開くとノードのカードが並ぶ()
        {
            Assert.IsTrue(IsShown(Find<VisualElement>(rootVisualElement, "research-tree")));
            foreach (var guid in Kit.AllNodes) Assert.IsTrue(IsShown(Card(guid)), guid.ToString());
            Assert.AreEqual("Research", Find<VisualElement>(rootVisualElement, "research-panel").Q<Label>(className: "game-panel__title").text);
        }

        /// <web-test file="src/features/research/ResearchNodeCard.test.ts">枠色用のdata属性と状態ラベルは%sで揃って付く</web-test>
        /// <web-test file="e2e/tests/research/research.spec.ts">ノードカードが4状態のdata属性で描き分けられる</web-test>
        [TestCase("00000000-0000-0000-0000-000000000201", true, false, false, "Available")]
        [TestCase("00000000-0000-0000-0000-000000000202", false, true, false, "Completed")]
        [TestCase("00000000-0000-0000-0000-000000000204", false, false, true, "Unavailable")]
        [TestCase("00000000-0000-0000-0000-000000000203", false, false, false, "Unavailable")]
        [TestCase("00000000-0000-0000-0000-000000000205", false, false, true, "Unavailable")]
        public void カードの枠の修飾と状態ラベルが状態で揃って付く(string guidText, bool ready, bool completed, bool locked, string stateLabel)
        {
            var guid = Guid.Parse(guidText);
            var card = Card(guid);
            Assert.AreEqual(ready, card.ClassListContains("research-tree__card--ready"), "ready");
            Assert.AreEqual(completed, card.ClassListContains("research-tree__card--completed"), "completed");
            Assert.AreEqual(locked, card.ClassListContains("research-tree__card--locked"), "locked");
            Assert.AreEqual(stateLabel, Find<Label>(rootVisualElement, $"research-node-state-{guid}").text);

            // ロックだけ減光する
            // Only locked cards dim
            Assert.AreEqual(locked, card.resolvedStyle.opacity < 1f, "opacity");
        }

        /// <web-test file="src/features/research/ResearchNodeCard.test.ts">状態ラベルはnodeState classNameでアイコン直下に描く</web-test>
        [Test]
        public void 状態ラベルはアイコンの直下に描く()
        {
            var card = Card(Kit.Completed);
            Assert.IsTrue(card[0].ClassListContains("research-tree__card-name"));
            Assert.IsTrue(card[1].ClassListContains("research-tree__card-icon"));
            var state = Find<Label>(rootVisualElement, $"research-node-state-{Kit.Completed}");
            Assert.AreSame(card[2], state);
            Assert.GreaterOrEqual(state.worldBound.yMin, card[1].worldBound.yMax - 0.5f);

            // カードの枠は中身の高さを持つ（包みの高さ 0 で Yoga に潰されない）
            // The card box takes its content height (not crushed by Yoga under a zero-height wrapper)
            Assert.LessOrEqual(state.worldBound.yMax, card.worldBound.yMax + 0.5f, "card holds its content");
            // Web の高さ: 枠 1+上 8+名前 21.7+6+アイコン 48+6+状態 18.6+下 8+枠 1＝118.3（行高は Mantine の body の 1.55 を継ぐ。CSS からの計算で実測ではない）
            // Web height: border 1 + top 8 + name 21.7 + 6 + icon 48 + 6 + state 18.6 + bottom 8 + border 1 = 118.3 (line height 1.55 inherited from the Mantine body; computed from CSS, not measured)
            Assert.AreEqual(118.3f, card.worldBound.height, 1f, "Web card height");
        }

        /// <web-test file="e2e/tests/research/research.spec.ts">研究パネルは持ち物の右隣から画面端までを占有し持ち物と重ならない</web-test>
        [Test]
        public void 研究エリアは持ち物の幅と列の間隔の右から舞台の右端と上下端までを占める()
        {
            var area = Find<VisualElement>(rootVisualElement, "research-tree").worldBound;
            var stage = _layer.worldBound;
            Assert.AreEqual(InventoryPanelWidth + StageColumnGap, area.xMin - stage.xMin, 1f, "left");
            Assert.AreEqual(stage.xMax, area.xMax, 1.5f, "right");
            Assert.AreEqual(stage.yMin, area.yMin, 1.5f, "top");
            Assert.AreEqual(stage.yMax, area.yMax, 1.5f, "bottom");

            // 持ち物側はエリアに奪われない
            // Inventory side is not taken
            Assert.AreEqual(PickingMode.Ignore, Find<VisualElement>(rootVisualElement, "research-tree").pickingMode);
            Assert.IsNull(rootVisualElement.panel.Pick(new Vector2(area.xMin - 20f, area.center.y))?.GetFirstAncestorOfType<ResearchTreeView>());
        }

        /// <web-test file="src/features/research/ResearchTreePanel.test.ts">選択トグルで詳細ペインが開閉しrenderNodeが更新される</web-test>
        [Test]
        public void 選択があれば詳細ペインを出してカードを選択の印にし無くなれば閉じる()
        {
            Assert.IsFalse(DetailPaneShown());

            _view.Render(Kit.Tree(ResearchSelection.Of(Kit.Researchable)));
            simulate.FrameUpdate();
            Assert.IsTrue(DetailPaneShown());
            Assert.IsTrue(Card(Kit.Researchable).ClassListContains("research-tree__card--selected"));

            // 研究ボタンは本文の幅いっぱい（Web は幅の指定なしで 300 − 枠 2 − craft の padding 25 − 本文 20 ＝ 253。D-21）
            // The research button fills the body width (Web has no width: 300 − border 2 − craft padding 25 − body 20 = 253; D-21)
            Assert.AreEqual(253f, Find<VisualElement>(rootVisualElement, $"research-button-{Kit.Researchable}").worldBound.width, 0.5f, "button width");

            _view.Render(Kit.Tree(ResearchSelection.None));
            simulate.FrameUpdate();
            Assert.IsFalse(DetailPaneShown());
            Assert.IsFalse(Card(Kit.Researchable).ClassListContains("research-tree__card--selected"));
        }

        [Test]
        public void 文字の上はカードの根が拾いアイコンの上はツールチップのためにアイコンが拾う()
        {
            // 文字は拾わずカードの根へ、アイコンは Web の ItemSlot のホバーの名前のために自分で拾う（押下の浮上は視野の L1 のタップが見る）
            // Text falls through to the card root; the icon picks for the Web ItemSlot hover name (bubbling of presses is covered by the viewport tap L1)
            var card = Card(Kit.Researchable);
            Assert.AreSame(card, rootVisualElement.panel.Pick(card[0].worldBound.center));
            Assert.AreSame(card, rootVisualElement.panel.Pick(card[2].worldBound.center));
            var onIcon = rootVisualElement.panel.Pick(card[1].worldBound.center);
            Assert.AreNotSame(card, onIcon);
            Assert.AreSame(card, onIcon.GetFirstAncestorOfType<Client.Ui.Screens.Research.Parts.ResearchNodeCard>());
        }

        /// <web-test file="e2e/tests/research/research.spec.ts">研究パネル展開中も採掘進捗バーが遮蔽されない（.viewportOverlayのz封じ込めに依存する回帰ガード）</web-test>
        [Test]
        public void 採掘の進捗バーは研究エリアより上の層にあり研究エリアに遮られない()
        {
            // 本番と同じ層に進捗HUDを出す
            // Show progress HUD on the production layer
            var progress = new ProgressHudView();
            progress.SetShown(true);
            _layers.GetLayer(UiLayer.Chrome, UiFamily.Viewport).Add(progress);
            simulate.FrameUpdate();
            var bar = Find<VisualElement>(rootVisualElement, "progress-bar");
            var center = bar.worldBound.center;

            // 研究エリアがバーの中心を覆っていなければ層の順序を見たことにならない
            // Unless the research area covers the bar centre, the layer order is not being tested
            Assert.IsTrue(Find<VisualElement>(rootVisualElement, "research-tree").worldBound.Contains(center), "area covers the bar");

            // HUD は拾わないので、Web の expectHitTestWithin と同じく判定の間だけ根を拾う設定にして戻す
            // The HUD ignores picking, so flip its root to Position only for the check and back, like Web expectHitTestWithin
            bar.pickingMode = PickingMode.Position;
            var hit = rootVisualElement.panel.Pick(center);
            bar.pickingMode = PickingMode.Ignore;
            Assert.IsTrue(hit == bar || bar.Contains(hit), hit?.name);
        }

        private VisualElement Card(Guid guid)
        {
            return Find<VisualElement>(rootVisualElement, $"research-node-{guid}");
        }

        private bool DetailPaneShown()
        {
            // 詳細ペインは最初の詳細で初めて載る
            // The detail pane is mounted on the first details
            var pane = rootVisualElement.Q("research-detail-pane");
            return pane != null && IsShown(pane);
        }
    }
}
