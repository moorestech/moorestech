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
        public void 左押下は設定を送り掴みが空なら操作の側で良性に断る()
        {
            var view = Render();
            Commands.SetNextResult(new Client.Ui.State.Commands.UiCommandRejected(Client.Ui.State.Commands.UiCommandRejectReason.FilterSplitterGrabEmpty, string.Empty));
            simulate.Click(Find<VisualElement>(view, "filter-slot-1-0"), MouseButton.LeftMouse);
            CollectionAssert.AreEqual(new[] { "filter_splitter.set_filter_item 1 0 set" }, Commands.Issued);
            CollectionAssert.IsEmpty(ShownToasts, "掴みが空は良性（Web の noop）");
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
