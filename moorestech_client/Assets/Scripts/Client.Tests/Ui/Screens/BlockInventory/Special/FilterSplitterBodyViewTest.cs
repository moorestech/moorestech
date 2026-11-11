using Client.Tests.Ui.Screens.BlockInventory.Special.Support;
using Client.Ui.Screens.BlockInventory.Special.FilterSplitter;
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
