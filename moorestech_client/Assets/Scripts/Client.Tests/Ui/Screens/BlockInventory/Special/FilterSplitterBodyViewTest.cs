using System.Linq;
using Client.Localization;
using Client.Tests.Ui.TestHost;
using Client.Ui.State.BlockInventory.Logic.Special;
using Client.Ui.State.BlockInventory.Special.Details;
using Core.Master;
using Mooresmaster.Localization.Generated;
using Client.Tests.Ui.Screens.BlockInventory.Special.Support;
using Client.Ui.Components.Controls;
using Client.Ui.Screens.BlockInventory.Special.FilterSplitter;
using Client.Ui.Screens.BlockInventory.Special.Registration;
using Mooresmaster.Model.BlocksModule;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.BlockInventory.Special
{
    /// <summary>
    /// フィルタ分配器: 方向ごとのモードの明示の送信・枠の右押下の解除・短い窓で末尾の枠に届く〈Task 9 で足す〉（Web filterSplitter.spec.ts）
    /// Filter splitter: explicit mode per direction, right-down clears a slot, the last slot is reachable on a short window (Web filterSplitter.spec.ts)
    /// </summary>
    public class FilterSplitterBodyViewTest : SpecialScreenTestHost
    {
        /// <web-test file="e2e/tests/block/filterSplitter.spec.ts">mode switch sends explicitly selected mode</web-test>
        [Test]
        public void 択一でブラックリストを選ぶとそのモードを明示して送る()
        {
            var view = Render();
            Assert.IsTrue(IsSelected(view, "filter-mode-0-Whitelist"));
            simulate.Click(Find<Button>(view, "filter-mode-0-Blacklist"));
            CollectionAssert.AreEqual(new[] { "filter_splitter.set_mode 0 Blacklist" }, Commands.Issued);
        }

        /// <web-test file="e2e/tests/block/filterSplitter.spec.ts">right click on a filter slot sends clear</web-test>
        [Test]
        public void 枠の右押下は解除を送る()
        {
            var view = Render();
            simulate.Click(Find<VisualElement>(view, "filter-slot-0-0"), MouseButton.RightMouse);
            CollectionAssert.AreEqual(new[] { "filter_splitter.set_filter_item 0 0 clear" }, Commands.Issued);
        }

        [Test]
        public void 左押下は設定を送る()
        {
            // 掴みが空の良性の断りは操作の側が決めてトーストへ渡す（SpecialBlockCommandsTest が見る）
            // The benign empty-grab refusal is decided and toasted by the command (covered by SpecialBlockCommandsTest)
            var view = Render();
            simulate.Click(Find<VisualElement>(view, "filter-slot-1-0"), MouseButton.LeftMouse);
            CollectionAssert.AreEqual(new[] { "filter_splitter.set_filter_item 1 0 set" }, Commands.Issued);
        }

        [Test]
        public void 日本語の実際の文で出口を左正面右と出しモード名を言い換える()
        {
            Localize.TrySetChosenLanguage("japanese");
            var view = Render();

            // 見本は出荷マスタと同じ 左・正面・右 の並び
            // The sample follows the shipped master order: left, front, right
            Assert.AreEqual("左", TextOf(view, "filter-direction-label-0"));
            Assert.AreEqual("正面", TextOf(view, "filter-direction-label-1"));
            Assert.AreEqual("右", TextOf(view, "filter-direction-label-2"));
            Assert.AreEqual("その他すべて", ModeLabelOf(view, "filter-mode-0-Default"));
            Assert.AreEqual("指定した物だけ", ModeLabelOf(view, "filter-mode-0-Whitelist"));
            Assert.AreEqual("指定した物以外", ModeLabelOf(view, "filter-mode-0-Blacklist"));
        }

        [Test]
        public void 見出しは番号でなく届いた側で決まり同じ数の別の分岐器でも書き換わる()
        {
            // テストマスタと同じ 右・左・正面 の並びを描き、続けて同じ方向数の 左・正面・右 を描く（方向の View は数が同じなら使い回される）
            // Render the test master order right, left, front, then a same-count left, front, right (direction views are reused when the count matches)
            var view = Mount(new FilterSplitterBodyView(Kit(LoadContent())));
            view.Render(NoSlots, SpecialDetailSamples.Only(Detail(FilterSplitterOutputSide.Right, FilterSplitterOutputSide.Left, FilterSplitterOutputSide.Front)));
            simulate.FrameUpdate();
            Assert.AreEqual(Localize.Get(LocalizationKeys.Ui.BlockInventory.FilterOutputRight), TextOf(view, "filter-direction-label-0"));
            Assert.AreEqual(Localize.Get(LocalizationKeys.Ui.BlockInventory.FilterOutputLeft), TextOf(view, "filter-direction-label-1"));
            Assert.AreEqual(Localize.Get(LocalizationKeys.Ui.BlockInventory.FilterOutputFront), TextOf(view, "filter-direction-label-2"));

            var firstDirection = Find<VisualElement>(view, "filter-direction-0");
            view.Render(NoSlots, SpecialDetailSamples.Only(SpecialDetailSamples.FilterSplitter));
            simulate.FrameUpdate();
            Assert.AreSame(firstDirection, Find<VisualElement>(view, "filter-direction-0"));
            Assert.AreEqual(Localize.Get(LocalizationKeys.Ui.BlockInventory.FilterOutputLeft), TextOf(view, "filter-direction-label-0"));
            Assert.AreEqual(Localize.Get(LocalizationKeys.Ui.BlockInventory.FilterOutputFront), TextOf(view, "filter-direction-label-1"));
            Assert.AreEqual(Localize.Get(LocalizationKeys.Ui.BlockInventory.FilterOutputRight), TextOf(view, "filter-direction-label-2"));
        }

        [TestCase("japanese")]
        [TestCase("english")]
        [TestCase("german")]
        [TestCase("korean")]
        public void モード名はどの言語でも択一のボタンに1行で収まる(string language)
        {
            // 本物のパネルの幅で開く（前例 短い窓でも末尾の方向の枠へスクロールで届き解除を送る）。折り返しは1行の高さを超えることで見る（前例 BuildMenuLayoutTest）
            // Open at the real panel width (precedent: the short-window test); wrapping shows as exceeding one line's height (precedent BuildMenuLayoutTest)
            Localize.TrySetChosenLanguage(language);
            Model.Set(SpecialDetailSamples.Only(SpecialDetailSamples.FilterSplitter));
            var shell = BlockInventoryTestShell.Create(this, Panels.State, new FilterSplitterBodyFactory(Kit(LoadContent()), Presenters));
            OpenPanel(BlockMasterElement.BlockTypeConst.FilterSplitter);
            simulate.FrameUpdate();
            simulate.FrameUpdate();

            var viewport = Find<GameScrollView>(shell.View, "block-inventory-natural-scroll").contentViewport;
            foreach (var mode in new[] { "Default", "Whitelist", "Blacklist" })
            {
                // 測るのは表示の文（text）、失敗の文に出すのは印を外した原文（PlainText）
                // Measure the displayed text; report the tag-free source text on failure
                var button = Find<Button>(shell.View, $"filter-mode-0-{mode}");
                var label = button.Q<Label>(className: "mode-switch__label");
                // フォールバックの字形で行高が変わるため、幅制限のない同じ文を1行の基準にする
                // Fallback glyphs can change line height, so use the same text at unconstrained width as the one-line baseline
                var oneLine = label.MeasureTextSize(label.text, 0f, VisualElement.MeasureMode.Undefined, 0f, VisualElement.MeasureMode.Undefined);
                var actual = label.MeasureTextSize(label.text, label.contentRect.width, VisualElement.MeasureMode.Exactly, 0f, VisualElement.MeasureMode.Undefined).y;
                // 折り返さない設定でも文字が見えるよう、中央揃えの文字と面・視口の重なる幅を比べる
                // Check the centered text against the visible face and viewport even when wrapping is disabled
                var textBox = label.LocalToWorld(label.contentRect);
                var face = button.LocalToWorld(button.contentRect);
                var visibleLeft = Mathf.Max(textBox.xMin, face.xMin, viewport.worldBound.xMin);
                var visibleRight = Mathf.Min(textBox.xMax, face.xMax, viewport.worldBound.xMax);
                var visibleWidth = 2f * Mathf.Min(textBox.center.x - visibleLeft, visibleRight - textBox.center.x);
                var requiredWorldWidth = oneLine.x * (textBox.width / label.contentRect.width);
                TestContext.WriteLine($"{language} {mode} 「{label.PlainText()}」: required={requiredWorldWidth:F2}, visible={visibleWidth:F2}, face={face.width:F2}, viewport={viewport.worldBound.width:F2}");
                Assert.LessOrEqual(requiredWorldWidth, visibleWidth + 0.5f, $"{language} {mode} 「{label.PlainText()}」の文字が面または視口の外へ出た");
                Assert.AreEqual(oneLine.y, actual, 0.5f, $"{language} {mode} 「{label.PlainText()}」が折り返した");
            }
        }

        private static string ModeLabelOf(VisualElement root, string optionTestid)
        {
            return Find<Button>(root, optionTestid).Q<Label>(className: "mode-switch__label").PlainText();
        }

        private static FilterSplitterDetail Detail(params FilterSplitterOutputSide[] sides)
        {
            return new FilterSplitterDetail(sides.Select(side => new FilterSplitterDirectionDetail(side, FilterSplitterMode.Default, new[] { ItemMaster.EmptyItemId })).ToArray());
        }

        /// <web-test file="e2e/tests/block/filterSplitter.spec.ts">last direction's filter slot is reachable by scrolling on a short window</web-test>
        [Test]
        public void 短い窓でも末尾の方向の枠へスクロールで届き解除を送る()
        {
            // alpha の外殻の土台に本物の本文の工場を差し、Web の e2e と同じ高さ 600 の舞台で開く
            // Plug the real body factory into alpha's shell fixture and open it on a 600px-tall stage, as in the Web e2e
            Model.Set(SpecialDetailSamples.Only(SpecialDetailSamples.FilterSplitter));
            var shell = BlockInventoryTestShell.Create(this, Panels.State, new FilterSplitterBodyFactory(Kit(LoadContent()), Presenters));
            shell.Stage.style.height = 600f;
            OpenPanel(BlockMasterElement.BlockTypeConst.FilterSplitter);
            simulate.FrameUpdate();
            simulate.FrameUpdate();

            // 空振りを防ぐ: 帯が 600 に追い、中身が窓を超え、末尾の枠がスクロール前は窓の外にある（720 では全部収まりスクロールしない）
            // Guard against a vacuous pass: the band follows 600, the content overflows and the last slot starts outside the viewport (at 720 everything fits)
            Assert.LessOrEqual(Find<VisualElement>(shell.View, "block-inventory-panel").worldBound.yMax, 600.5f, "パネルの帯が高さ 600 の舞台に追う");
            var scroll = Find<GameScrollView>(shell.View, "block-inventory-natural-scroll");
            Assert.Greater(scroll.GetContentHeight(), scroll.GetViewportHeight(), "高さ 600 で中身が窓を超える");
            var last = Find<VisualElement>(shell.View, "filter-slot-2-1");
            Assert.IsFalse(IsFullyInside(scroll, last), "スクロール前は末尾の枠が窓の外");

            // 末尾までスクロールすると枠が窓に入り、右押下で解除を送る
            // Scrolling to the end brings the slot into view and a right press sends clear
            scroll.SetScrollOffset(scroll.GetContentHeight() - scroll.GetViewportHeight());
            simulate.FrameUpdate();
            Assert.IsTrue(IsFullyInside(scroll, last), "スクロールの後に末尾の枠が見える");
            simulate.Click(last, MouseButton.RightMouse);
            Assert.AreEqual("filter_splitter.set_filter_item 2 1 clear", Commands.Issued[Commands.Issued.Count - 1]);
        }

        private static bool IsFullyInside(VisualElement viewport, VisualElement element)
        {
            return viewport.worldBound.yMin <= element.worldBound.yMin && element.worldBound.yMax <= viewport.worldBound.yMax;
        }

        private FilterSplitterBodyView Render()
        {
            var view = Mount(new FilterSplitterBodyView(Kit(LoadContent())));
            view.Render(NoSlots, SpecialDetailSamples.Only(SpecialDetailSamples.FilterSplitter));

            // 本文は載せた後の Render で中身を組むので、押下の前に配置を解く
            // The body builds its content in Render after mounting, so resolve layout before pressing
            simulate.FrameUpdate();
            return view;
        }
    }
}
