using Client.Tests.Ui.TestDoubles;
using Client.Ui.State.BlockInventory.Logic.Belt;
using NUnit.Framework;

namespace Client.Tests.Ui.State.BlockInventory.Belt
{
    /// <summary>
    /// Web の beltThroughputLogic.test.ts の写し（L0）
    /// Port of Web beltThroughputLogic.test.ts (L0)
    /// </summary>
    public class BeltThroughputLogicTest
    {
        private static readonly BeltThroughputWindow[] Windows =
        {
            BeltThroughputWindow.Measured(90, 4, 4.5),
            BeltThroughputWindow.Measured(180, 9, 9),
            BeltThroughputWindow.Measuring(360, 18),
        };

        /// <web-test file="src/features/blockInventory/details/__tests__/beltThroughputLogic.test.ts">selects the window by length and falls back to the first</web-test>
        [Test]
        public void 窓を長さで選び一覧に無ければ先頭へ落とす()
        {
            Assert.IsTrue(BeltThroughputLogic.TrySelectBeltWindow(Windows, 180, out var selected));
            Assert.AreEqual(180, selected.Ticks);
            Assert.IsTrue(BeltThroughputLogic.TrySelectBeltWindow(Windows, 5760, out var fallback));
            Assert.AreEqual(90, fallback.Ticks);
            Assert.IsFalse(BeltThroughputLogic.TrySelectBeltWindow(new BeltThroughputWindow[0], 180, out _));
        }

        /// <web-test file="src/features/blockInventory/details/__tests__/beltThroughputLogic.test.ts">cycles one window at a time without wrapping</web-test>
        [Test]
        public void 窓は1つずつ送り端で止まり一覧に無い長さは先頭扱い()
        {
            Assert.AreEqual(360, BeltThroughputLogic.CycleBeltWindow(Windows, 180, BeltWindowStep.Next));
            Assert.AreEqual(360, BeltThroughputLogic.CycleBeltWindow(Windows, 360, BeltWindowStep.Next));
            Assert.AreEqual(90, BeltThroughputLogic.CycleBeltWindow(Windows, 180, BeltWindowStep.Previous));
            Assert.AreEqual(90, BeltThroughputLogic.CycleBeltWindow(Windows, 90, BeltWindowStep.Previous));
            Assert.AreEqual(180, BeltThroughputLogic.CycleBeltWindow(Windows, 5760, BeltWindowStep.Next));
            Assert.AreEqual(180, BeltThroughputLogic.CycleBeltWindow(new BeltThroughputWindow[0], 180, BeltWindowStep.Next));
        }

        /// <web-test file="src/features/blockInventory/details/__tests__/beltThroughputLogic.test.ts">formats a filled window with percent and per-minute</web-test>
        [Test]
        public void 埋まった窓は割合と毎分の数を出す()
        {
            // 180tick=9 秒に 9 個で 100%、分換算は 9×1200/180=60
            // 9 items over 180 ticks (9 s) is 100%, per minute 9×1200/180 = 60
            var row = BeltThroughputLogic.RowDisplay(Windows[1]);
            UiTextAssert.AreEqual("ui.blockInventory.beltThroughputWindow", new[] { "seconds", "9", "count", "9", "max", "9", "percent", "100" }, row.Text);
            Assert.AreEqual("60.0", ((MeasuredBeltRowDisplay)row).ItemsPerMinute);
        }

        /// <web-test file="src/features/blockInventory/details/__tests__/beltThroughputLogic.test.ts">keeps fractional seconds and maximums to one decimal</web-test>
        [Test]
        public void 端数の秒と最大は小数1桁で出す()
        {
            // 90tick は 4.5 秒・最大 4.5 個。4 個で 89%
            // 90 ticks is 4.5 s with a maximum of 4.5; 4 items is 89%
            var row = BeltThroughputLogic.RowDisplay(Windows[0]);
            UiTextAssert.AreEqual("ui.blockInventory.beltThroughputWindow", new[] { "seconds", "4.5", "count", "4", "max", "4.5", "percent", "89" }, row.Text);
            Assert.AreEqual("53.3", ((MeasuredBeltRowDisplay)row).ItemsPerMinute);
        }

        /// <web-test file="src/features/blockInventory/details/__tests__/beltThroughputLogic.test.ts">reports a measuring window without a per-minute figure</web-test>
        [Test]
        public void 計測中の窓は毎分の数を出さない()
        {
            var row = BeltThroughputLogic.RowDisplay(Windows[2]);
            UiTextAssert.AreEqual("ui.blockInventory.beltThroughputMeasuring", new[] { "seconds", "18" }, row.Text);
            Assert.IsInstanceOf<MeasuringBeltRowDisplay>(row);
        }

        /// <web-test file="src/features/blockInventory/details/__tests__/beltThroughputLogic.test.ts">shows zero percent for a stopped belt</web-test>
        [Test]
        public void 停止中のベルトは0パーセント()
        {
            var row = BeltThroughputLogic.RowDisplay(BeltThroughputWindow.Measured(180, 0, 0));
            UiTextAssert.AreEqual("ui.blockInventory.beltThroughputWindow", new[] { "seconds", "9", "count", "0", "max", "0", "percent", "0" }, row.Text);
        }

        /// <web-test file="src/features/blockInventory/details/__tests__/beltThroughputLogic.test.ts">maps per-second counts onto bar heights oldest first</web-test>
        [Test]
        public void 秒ごとの数を古い順に棒の高さへ写す()
        {
            // 足りない秒は先頭側を null で埋め、最大を超えた秒は 1 に丸め、停止中は 0
            // Missing seconds are null at the front, a second above the maximum rounds to 1, a stopped belt gives 0
            CollectionAssert.AreEqual(new double?[] { null, null, 0.5, 0, 1 }, BeltThroughputLogic.RecentSecondBars(new[] { 1, 0, 2 }, 2, 5));
            CollectionAssert.AreEqual(new double?[] { 1, 0.5 }, BeltThroughputLogic.RecentSecondBars(new[] { 3, 1 }, 2, 2));
            CollectionAssert.AreEqual(new double?[] { 0.5, 0.5 }, BeltThroughputLogic.RecentSecondBars(new[] { 1, 1, 1 }, 2, 2));
            CollectionAssert.AreEqual(new double?[] { null, 0 }, BeltThroughputLogic.RecentSecondBars(new[] { 1 }, 0, 2));
            CollectionAssert.AreEqual(new double?[] { null, null, null }, BeltThroughputLogic.RecentSecondBars(new int[0], 2, 3));
        }
    }
}
