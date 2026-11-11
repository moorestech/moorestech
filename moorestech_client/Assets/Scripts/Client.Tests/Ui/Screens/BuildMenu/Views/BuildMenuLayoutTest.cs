using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Client.Tests.Ui.Screens.BuildMenu.Support;
using Client.Ui.Components.Slots;
using Client.Ui.State.BuildMenu.Entries;
using Client.Ui.State.PlacementTarget;
using NUnit.Framework;
using Tests.Module.TestMod;
using UnityEngine;
using UnityEngine.UIElements;
using static Client.Tests.Ui.Screens.BuildMenu.Support.BuildMenuSamples;

namespace Client.Tests.Ui.Screens.BuildMenu.Views
{
    public class BuildMenuLayoutTest : BuildMenuScreenTestHost
    {
        private const string TokensPath = "Assets/Scripts/Client.Ui/Foundation/Theme/Tokens.uss";
        private const float StageHeight = 720f;

        /// <web-test file="e2e/tests/regression/buildMenuLayout.spec.ts">パネルは画面水平中央に表示される</web-test>
        [Test]
        public void パネルは画面の水平中央に出る()
        {
            OpenMenu(Create());

            Assert.AreEqual(View.worldBound.center.x, Find<VisualElement>(View, "build-menu-panel").worldBound.center.x, 1f);
        }

        /// <web-test file="e2e/tests/regression/buildMenuLayout.spec.ts">パネルは上部安全帯の直下にバンド高いっぱいで立つ</web-test>
        [Test]
        public void パネルは上の安全帯の直下に帯の高さいっぱいで立つ()
        {
            OpenMenu(Create());
            var panel = Find<VisualElement>(View, "build-menu-panel").worldBound;
            var frame = Find<VisualElement>(View, "build-menu-frame").worldBound;

            // 期待値はトークンから解く。帯の高さは Web の --menu-content-height（舞台の高さ − 上下の安全帯）
            // Expectations derive from tokens; the band height is the Web --menu-content-height (stage height minus both safe areas)
            var upper = TokenPixels("--menu-upper-safe-area");
            Assert.AreEqual(upper, panel.y - View.worldBound.y, 1f);
            Assert.AreEqual(StageHeight - upper - TokenPixels("--menu-lower-safe-area"), panel.height, 1f);
            Assert.AreEqual(TokenPixels("--build-menu-panel-width"), panel.width, 1f);
            Assert.AreEqual(panel.height, frame.height, 1f);
        }

        /// <web-test file="e2e/tests/regression/buildMenuLayout.spec.ts">カテゴリボタンは全ボタン同一の固定高</web-test>
        [Test]
        public void カテゴリボタンは全部同じ固定の高さ()
        {
            OpenMenu(Create());
            var expected = TokenPixels("--build-menu-category-height");

            var buttons = CategoryButtons();

            Assert.AreEqual(10, buttons.Length);
            foreach (var button in buttons) Assert.AreEqual(expected, button.layout.height, 1f, button.name);
        }

        /// <web-test file="e2e/tests/regression/buildMenuLayout.spec.ts">カテゴリサイドバーは全カテゴリとラベルを固定高のまま収める</web-test>
        [Test]
        public void カテゴリサイドバーは全カテゴリとラベルを固定の高さのまま収める()
        {
            OpenMenu(Create());

            AssertCategoryLabelsStayOneLine();
            var columns = Find<VisualElement>(View, "build-menu-columns");
            var contentBottom = columns.worldBound.yMax - columns.resolvedStyle.paddingBottom;
            Assert.LessOrEqual(CategoryButtons().Last().worldBound.yMax, contentBottom + 0.5f);
        }

        /// <web-test file="e2e/tests/regression/buildMenuLayout.spec.ts">英語ロケールでもカテゴリ名は1行のまま固定高に収まる</web-test>
        [Test]
        public void 英語でもカテゴリ名は1行のまま固定の高さに収まる()
        {
            OpenMenu(Create());

            // 最長の英語名が出ていることを先に固定し、空振りの合格を防ぐ
            // Pin the longest English name first to avoid a vacuous pass
            StringAssert.Contains("Building Materials", TextOf(CategoryButton(BuildingMaterials)));
            AssertCategoryLabelsStayOneLine();
        }

        /// <web-test file="e2e/tests/regression/buildMenuLayout.spec.ts">グリッドは8列を保ち中央列に収まる</web-test>
        [Test]
        public void 格子は8列を保ち中央の列に収まる()
        {
            OpenMenu(Create());
            var sections = Find<VisualElement>(View, "build-menu-sections");
            var grid = sections.Q<SlotGrid>();

            Assert.AreEqual(8, grid.Q(className: "slot-grid__tracks").childCount);

            // バーの帯の予約も固定し、8 列目にバーが重ならないことを守る
            // Pin the scrollbar reserve too so the bar never overlaps column 8
            Assert.Greater(sections.resolvedStyle.paddingRight, 0f);
            Assert.LessOrEqual(grid.layout.width, sections.contentRect.width + 0.5f);
        }

        /// <web-test file="e2e/tests/regression/buildMenuLayout.spec.ts">スクロール領域のクリップ境界をハイライト枠の逃げぶんセルから離す</web-test>
        [Test]
        public void スクロール領域のクリップ境界をハイライト枠の逃げぶんセルから離す()
        {
            OpenMenu(Create());
            var clip = Scroll.contentViewport.worldBound;
            var cell = View.Query<VisualElement>(className: "build-menu__entry").First().worldBound;

            // 逃げはリテラルでなく関係式で見る（Web と同じ。トークンが変わっても番人であり続ける）
            // Assert the relation, not literals (same as the Web), so it stays a guard when tokens change
            var required = TokenPixels("--tutorial-anchor-padding") + TokenPixels("--tutorial-highlight-glow");
            Assert.Greater(required, 0f);
            Assert.GreaterOrEqual(cell.x - clip.x, required - 0.01f);
            Assert.GreaterOrEqual(cell.y - clip.y, required - 0.01f);
        }

        /// <web-test file="e2e/tests/regression/placementIconCentering.spec.ts">ビルドメニューのアイコンは枠の中央に描かれる</web-test>
        [Test]
        public void ビルドメニューのアイコンは枠の中央に描かれる()
        {
            OpenMenu(Create(Array.Empty<BuildMenuMaterialRow>(), new PlacementTargetBlockIcon(ForUnitTestModBlockId.BlockId)));
            var slot = Entry("block", WoodChest).worldBound;
            var icon = Entry("block", WoodChest).Q(className: "placement-target-face__icon");
            Assert.AreEqual(DisplayStyle.Flex, icon.resolvedStyle.display);

            // 描画面は絵の要素から --icon-pad の内余白を除いた箱（Web の img の padding を除いた内容箱）
            // The drawn area is the icon element minus its --icon-pad padding (the Web img content box)
            var style = icon.resolvedStyle;
            var left = icon.worldBound.xMin + style.paddingLeft - slot.xMin;
            var right = slot.xMax - (icon.worldBound.xMax - style.paddingRight);
            var top = icon.worldBound.yMin + style.paddingTop - slot.yMin;
            var bottom = slot.yMax - (icon.worldBound.yMax - style.paddingBottom);
            TestContext.WriteLine($"build-menu margins left={left} right={right} top={top} bottom={bottom}");
            Assert.Greater(left, 0f);
            Assert.AreEqual(left, right, 0.5f);
            Assert.AreEqual(top, bottom, 0.5f);
            Assert.AreEqual(left, top, 0.5f);
        }

        // 折り返すと 1 行の高さを超える。Web の scrollHeight と clientHeight の比較に当たる
        // Wrapping exceeds the one-line height; stands for the Web scrollHeight vs clientHeight check
        private void AssertCategoryLabelsStayOneLine()
        {
            foreach (var button in CategoryButtons())
            foreach (var text in button.Query<TextElement>().ToList().Where(text => !string.IsNullOrEmpty(text.text)))
            {
                var oneLine = text.MeasureTextSize("A", 0f, VisualElement.MeasureMode.Undefined, 0f, VisualElement.MeasureMode.Undefined).y;
                var actual = text.MeasureTextSize(text.text, text.contentRect.width, VisualElement.MeasureMode.Exactly, 0f, VisualElement.MeasureMode.Undefined).y;
                Assert.AreEqual(oneLine, actual, 0.5f, button.name);
                Assert.LessOrEqual(actual, button.contentRect.height + 0.5f, button.name);
            }
        }

        // 期待値はトークンの本文から読む（前例 UssCustomStyleReaderTest の ReadTokenBody）
        // Read expectations from the token body (precedent: UssCustomStyleReaderTest.ReadTokenBody)
        private static float TokenPixels(string name)
        {
            var match = Regex.Match(File.ReadAllText(Path.Combine(Path.GetDirectoryName(Application.dataPath), TokensPath)), $@"{Regex.Escape(name)}:\s*(-?[0-9.]+)px");
            Assert.IsTrue(match.Success, name);
            return float.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
        }
    }
}
