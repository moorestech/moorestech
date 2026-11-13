using System;
using System.Linq;
using Client.Localization;
using Client.Tests.Ui.Components;
using Client.Ui.Components.Controls;
using Client.Ui.Foundation.Layers;
using Client.Ui.Screens.Challenge.Tree;
using Client.Ui.Screens.Inventory.Wheel;
using Client.Ui.State.Challenge.Tree;
using Core.Master;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;
using Kit = Client.Tests.Ui.Screens.Challenge.Tree.ChallengeListScreenTestKit;

namespace Client.Tests.Ui.Screens.Challenge.Tree
{
    /// <summary>
    /// チャレンジ一覧の表示（L1）
    /// Challenge list display (L1)
    /// </summary>
    public class ChallengeListViewTest : ScreenTestHost
    {
        private ChallengeListView _view;

        [SetUp]
        public void SetUp()
        {
            Kit.LoadMasterAndDictionary();
            var layers = UiLayerStack.Build(rootVisualElement);
            _view = Kit.MountList(layers, new RecordingChallengeListCommands());
            Kit.MountHud(layers);
            Settle();
        }

        /// <web-test file="e2e/tests/challenge.spec.ts">チャレンジ画面が開きツリーだけを翻訳済み表示する</web-test>
        [Test]
        public void チャレンジ画面が開きツリーだけを翻訳済み表示する()
        {
            // Web と同じく日本語で見る
            // Read it in Japanese like the Web
            Localize.TrySetChosenLanguage("japanese");
            Settle();

            // 枠・ボタン・カード 2 枚・題が出る
            // Frame, button, two cards and title show
            Assert.IsTrue(IsShown(Find<VisualElement>(rootVisualElement, "challenge-panel")));
            var basics = Find<FormButton>(rootVisualElement, $"challenge-category-{Kit.Basics}");
            Assert.AreEqual(MasterHolder.ChallengeMaster.GetChallengeCategory(Kit.Basics).CategoryName, Kit.ButtonText(basics));
            Assert.IsTrue(IsShown(Kit.Card(rootVisualElement, Kit.FirstCraft)));
            Assert.IsTrue(IsShown(Kit.Card(rootVisualElement, Kit.SecondStep)));
            Assert.AreEqual("チャレンジ", Kit.PlainText(_view.Q<Label>(className: "challenge-list__title")));

            // 和文の題の行は Web の Title order=2（26px×1.35）で、カテゴリ列はその直下（字の自然な高さ 38px で下へずれない）
            // The Japanese title row is the Web Title order=2 (26px x 1.35) and the category row sits right below it (not pushed down by the 38px natural glyph height)
            var title = _view.Q<Label>(className: "challenge-list__title");
            Assert.AreEqual(35.1f, title.layout.height, 0.5f, "題の行の高さ");
            Assert.AreEqual(title.worldBound.yMax, Find<VisualElement>(rootVisualElement, "challenge-categories").worldBound.yMin, 0.5f, "カテゴリ列の上端");

            // 名前と状態は翻訳済み
            // Titles and states are localized
            var first = Kit.Card(rootVisualElement, Kit.FirstCraft);
            Assert.AreEqual(MasterHolder.ChallengeMaster.GetChallenge(Kit.FirstCraft).Title, Kit.PlainText(first.Q<Label>(className: "challenge-list__card-title")));
            var states = _view.Query<Label>(className: "challenge-list__card-state").ToList().Select(Kit.PlainText).ToList();
            CollectionAssert.Contains(states, "完了");
            CollectionAssert.Contains(states, "進行中");

            // 内部キー・欠けの印が無く HUD も出る
            // No internal keys or missing markers; HUD shows
            foreach (var label in _view.Query<Label>().ToList())
            {
                StringAssert.DoesNotContain("challenge.", label.text);
                StringAssert.DoesNotContain("[!", label.text);
            }
            Assert.IsTrue(IsShown(Find<VisualElement>(rootVisualElement, "challenge-hud")));
        }

        [Test]
        public void カードは中身なりの高さで幅240と状態の輪と減光を持つ()
        {
            // 包みの高さ 0 で潰れず、上下の余白 12 と 3 行の高さの和になる（S4-research の NodeGraphView.uss の事後承認）
            // Not crushed by a zero-height wrapper: 12 padding on each side plus three rows (post-approval of S4-research's NodeGraphView.uss)
            var padding = ThemeTokenFile.ResolvePixels("--mantine-spacing-sm");
            foreach (var guid in new[] { Kit.FirstCraft, Kit.SecondStep, Kit.LockedStep })
            {
                var card = Kit.Card(rootVisualElement, guid);
                var rows = card.Query<Label>().ToList().Sum(label => label.layout.height);
                Assert.AreEqual(240f, card.layout.width, 0.5f, $"{guid} の幅");
                Assert.AreEqual(rows + padding * 2f, card.layout.height, 0.5f, $"{guid} の高さ");
                Assert.GreaterOrEqual(card.layout.height, 24.8f + 20.3f + 16.8f + padding * 2f - 0.5f, $"{guid} が 3 行の最小より低い");
            }

            // 完了 green-6・進行中 blue-5 の輪
            // Completed green-6 and current blue-5 rings
            AssertRing(Kit.FirstCraft, "--mantine-color-green-6");
            AssertRing(Kit.SecondStep, "--mantine-color-blue-5");
            var locked = Kit.Card(rootVisualElement, Kit.LockedStep);
            Assert.AreEqual(DisplayStyle.None, locked.Q(className: "challenge-list__card-ring").resolvedStyle.display);
            Assert.AreEqual(0.55f, locked.resolvedStyle.opacity, 0.001f);

            // 面は Paper・地は dark-8
            // Face is Paper, ground is dark-8
            ThemeTokenFile.AssertSameColor(ThemeTokenFile.ResolveColor("--mantine-color-body"), Kit.Card(rootVisualElement, Kit.FirstCraft).resolvedStyle.backgroundColor, "カードの面");
            var panel = Find<VisualElement>(rootVisualElement, "challenge-panel");
            ThemeTokenFile.AssertSameColor(ThemeTokenFile.ResolveColor("--mantine-color-dark-8"), panel.resolvedStyle.backgroundColor, "一覧の地");
            Assert.AreEqual(ThemeTokenFile.ResolvePixels("--menu-upper-safe-area"), panel.worldBound.yMin, 0.5f);
        }

        [Test]
        public void 解放済みのカテゴリが無ければ題だけでグラフを置かない()
        {
            _view.Render(ChallengeListViewModel.Empty);
            Settle();
            Assert.IsNull(rootVisualElement.Q("challenge-viewport"));
            Assert.AreEqual(0, rootVisualElement.Query<FormButton>().ToList().Count);
            Assert.IsTrue(IsShown(_view.Q<Label>(className: "challenge-list__title")));
        }

        [Test]
        public void カテゴリのボタンが幅を越えたら横に送れ越えなければバーが出ない()
        {
            // ボタンは幅に収まりバー無し
            // Buttons fit the width, no bar
            var row = Find<VisualElement>(rootVisualElement, "challenge-categories");
            var scroll = Find<GameScrollView>(rootVisualElement, "challenge-categories-scroll");
            Assert.IsFalse(IsShown(scroll.horizontalScroller), "収まるときのバー");
            Assert.AreEqual(36f, row.layout.height, 0.5f, "収まるときの列の高さ");

            // 120px に狭めるとバーが出る（Web overflow-x: auto）
            // Narrowing to 120px shows the bar (Web overflow-x: auto)
            row.style.width = 120f;
            Settle();
            Assert.IsTrue(IsShown(scroll.horizontalScroller), "越えたときのバー");
            var last = Find<FormButton>(rootVisualElement, $"challenge-category-{Kit.Logistics}");
            Assert.Greater(last.worldBound.xMax, scroll.contentViewport.worldBound.xMax, "送る前は末尾のボタンがはみ出す");
            scroll.ScrollTo(last);
            Settle();
            Assert.LessOrEqual(last.worldBound.xMax, scroll.contentViewport.worldBound.xMax + 0.5f, "送ると末尾のボタンが見える");

            // バーは GameScrollView のバーと同じ
            // The bar matches the GameScrollView bar
            var barSize = ThemeTokenFile.ResolvePixels("--scrollarea-scrollbar-size");
            Assert.AreEqual(barSize, scroll.horizontalScroller.layout.height, 0.5f, "バーの太さ");
            var thumb = scroll.horizontalScroller.Q(className: "unity-base-slider__dragger");
            ThemeTokenFile.AssertSameColor(ThemeTokenFile.ResolveColor("--game-scroll-view-thumb-face"), thumb.resolvedStyle.backgroundColor, "つまみの色");
            Assert.AreEqual(36f + barSize, row.layout.height, 0.5f, "越えたときの列の高さ");
        }

        [Test]
        public void 一覧の題とカテゴリ列の上のホイールは装備とホットバーへ抜けない()
        {
            // 装備・ホットバーのホイールの読み手と同じ判定（EquipmentWheelInput は拾った要素を IsBlockingUi に通す）
            // The same judge the equipment/hotbar wheel reader uses (EquipmentWheelInput runs the picked element through IsBlockingUi)
            var title = _view.Q<Label>(className: "challenge-list__title");
            var row = Find<VisualElement>(rootVisualElement, "challenge-categories");
            var basics = Find<FormButton>(rootVisualElement, $"challenge-category-{Kit.Basics}");
            var rowEnd = new Vector2(row.worldBound.xMax - 8f, row.worldBound.center.y);
            foreach (var (where, point) in new[] { ("題", title.worldBound.center), ("カテゴリのボタン", basics.worldBound.center), ("カテゴリ列の空き", rowEnd) })
            {
                var picked = rootVisualElement.panel.Pick(point);
                Assert.IsTrue(EquipmentWheelPickJudge.IsBlockingUi(picked), $"{where} の上のホイールが抜ける（当たり={picked?.name}）");
            }
        }

        private void AssertRing(Guid guid, string colorToken)
        {
            var ring = Kit.Card(rootVisualElement, guid).Q(className: "challenge-list__card-ring");
            Assert.AreEqual(DisplayStyle.Flex, ring.resolvedStyle.display, $"{guid} の輪");
            Assert.AreEqual(2f, ring.resolvedStyle.borderTopWidth, 0.01f, $"{guid} の輪の幅");
            ThemeTokenFile.AssertSameColor(ThemeTokenFile.ResolveColor(colorToken), ring.resolvedStyle.borderTopColor, $"{guid} の輪の色");
        }

        // 中央寄せと逃げの読み取り・バーの出し入れの数フレームを流す
        // Run a few frames for clearance reads, centering and bar toggling
        private void Settle()
        {
            for (var i = 0; i < 4; i++) simulate.FrameUpdate();
        }
    }
}
