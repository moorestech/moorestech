using System.Collections.Generic;
using Client.Tests.Ui.TestDoubles;
using Client.Ui.State.BlockInventory.Logic.CleanRoom;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;

namespace Client.Tests.Ui.State.BlockInventory.CleanRoom
{
    /// <summary>
    /// 部屋欄の行（室内機械は部屋クラスだけ、清浄機は台数つき。ADR 0105 §1・§5）
    /// Room section rows (class only for machines; counts for purifiers; ADR 0105 §1, §5)
    /// </summary>
    public class CleanRoomLogicTest
    {
        [Test]
        public void 室内機械は部屋クラスの1行だけを出す()
        {
            var view = CleanRoomLogic.Display(CleanRoomFetchedDetail.Enclosed(CleanRoomSummary.ForMachine(CleanRoomHeadline.Class("C")), CleanRoomHaltReason.None));

            Assert.IsInstanceOf<HeadlineOnlyCleanRoomDisplay>(view);
            UiTextAssert.AreEqual(LocalizationKeys.Ui.BlockInventory.CleanRoomClass, new[] { "className", "C" }, view.Headline);
            Assert.IsFalse(view.HeadlineInsufficient);
            Assert.IsFalse(view.TryGetHaltReason(out _));
        }

        [Test]
        public void 室内機械のOutは赤で示し停止理由を出す()
        {
            var view = CleanRoomLogic.Display(CleanRoomFetchedDetail.Enclosed(CleanRoomSummary.ForMachine(CleanRoomHeadline.ClassOut), CleanRoomHaltReason.ClassOut));

            Assert.IsInstanceOf<HeadlineOnlyCleanRoomDisplay>(view);
            UiTextAssert.AreEqual(LocalizationKeys.Ui.BlockInventory.CleanRoomClassOut, view.Headline);
            Assert.IsTrue(view.HeadlineInsufficient);
            Assert.IsTrue(view.TryGetHaltReason(out var haltReason));
            UiTextAssert.AreEqual(LocalizationKeys.Ui.BlockInventory.CleanRoomHaltClassOut, haltReason);
        }

        [Test]
        public void 清浄機は次のクラスまでの台数つきの見出しと台数とこの清浄機を出す()
        {
            var status = CleanRoomAirFilterRoomStatus.WithUnitsToNextClass(6, 0, 0.756, 0.5, "B", 5);
            var view = (AirFilterCleanRoomDisplay)CleanRoomLogic.Display(CleanRoomFetchedDetail.Enclosed(CleanRoomSummary.ForAirFilter(CleanRoomHeadline.Class("C"), status), CleanRoomHaltReason.None));

            UiTextAssert.AreEqual(LocalizationKeys.Ui.BlockInventory.CleanRoomPurifierClassUnitsToNext, new[] { "className", "C", "nextClassName", "B", "units", "5" }, view.Headline);
            Assert.IsFalse(view.HeadlineInsufficient);
            UiTextAssert.AreEqual(LocalizationKeys.Ui.BlockInventory.CleanRoomAirFilterCount, new[] { "count", "6" }, view.AirFilterCount);
            UiTextAssert.AreEqual(LocalizationKeys.Ui.BlockInventory.CleanRoomQueriedAirFilter, new[] { "power", "76", "remaining", "50" }, view.QueriedAirFilter);
            Assert.IsFalse(view.TryGetHaltReason(out _));
        }

        [Test]
        public void 台数で次のクラスへ届かない清浄機の見出しはクラスだけ()
        {
            var status = CleanRoomAirFilterRoomStatus.WithoutUnitsToNextClass(3, 0, 1, 1);
            var view = CleanRoomLogic.Display(CleanRoomFetchedDetail.Enclosed(CleanRoomSummary.ForAirFilter(CleanRoomHeadline.Class("A"), status), CleanRoomHaltReason.None));

            UiTextAssert.AreEqual(LocalizationKeys.Ui.BlockInventory.CleanRoomPurifierClass, new[] { "className", "A" }, view.Headline);
        }

        [Test]
        public void クラス未達の清浄機は赤で_台数で届くなら次のクラスまでの台数を添える()
        {
            var withUnits = CleanRoomAirFilterRoomStatus.WithUnitsToNextClass(1, 0, 1, 1, "D", 2);
            var view = CleanRoomLogic.Display(CleanRoomFetchedDetail.Enclosed(CleanRoomSummary.ForAirFilter(CleanRoomHeadline.ClassOut, withUnits), CleanRoomHaltReason.None));
            UiTextAssert.AreEqual(LocalizationKeys.Ui.BlockInventory.CleanRoomPurifierClassOutUnitsToNext, new[] { "nextClassName", "D", "units", "2" }, view.Headline);
            Assert.IsTrue(view.HeadlineInsufficient);

            var withoutUnits = CleanRoomAirFilterRoomStatus.WithoutUnitsToNextClass(1, 0, 1, 1);
            var unreachable = CleanRoomLogic.Display(CleanRoomFetchedDetail.Enclosed(CleanRoomSummary.ForAirFilter(CleanRoomHeadline.ClassOut, withoutUnits), CleanRoomHaltReason.None));
            UiTextAssert.AreEqual(LocalizationKeys.Ui.BlockInventory.CleanRoomClassOut, unreachable.Headline);
            Assert.IsTrue(unreachable.HeadlineInsufficient);
        }

        [Test]
        public void 止まった清浄機があれば台数の行にうち何台かを添える()
        {
            var status = CleanRoomAirFilterRoomStatus.WithoutUnitsToNextClass(6, 1, 0, 0.25);
            var view = (AirFilterCleanRoomDisplay)CleanRoomLogic.Display(CleanRoomFetchedDetail.Enclosed(CleanRoomSummary.ForAirFilter(CleanRoomHeadline.Class("C"), status), CleanRoomHaltReason.None));

            UiTextAssert.AreEqual(LocalizationKeys.Ui.BlockInventory.CleanRoomAirFilterCountStopped, new[] { "count", "6", "stopped", "1" }, view.AirFilterCount);
            UiTextAssert.AreEqual(LocalizationKeys.Ui.BlockInventory.CleanRoomQueriedAirFilter, new[] { "power", "0", "remaining", "25" }, view.QueriedAirFilter);
        }

        private static IEnumerable<TestCaseData> UnsealedCases()
        {
            yield return new TestCaseData(CleanRoomFetchedDetail.Openings(3, CleanRoomHaltReason.NotEnclosed), "ui.blockInventory.cleanRoomNotSealed", new[] { "count", "3" }, true).SetName("未密閉_開口あり");
            yield return new TestCaseData(CleanRoomFetchedDetail.NoEnclosure(CleanRoomHaltReason.NotEnclosed), "ui.blockInventory.cleanRoomNoEnclosure", new string[0], true).SetName("未密閉_囲い無し");
            yield return new TestCaseData(CleanRoomFetchedDetail.TooLarge(4096, CleanRoomHaltReason.NotEnclosed), "ui.blockInventory.cleanRoomTooLarge", new[] { "max", "4096" }, true).SetName("未密閉_大きすぎ");
            yield return new TestCaseData(CleanRoomFetchedDetail.Detecting(CleanRoomHaltReason.NotEnclosed), "ui.blockInventory.cleanRoomDetecting", new string[0], false).SetName("未密閉_検出中");
        }

        /// <web-test file="src/features/blockInventory/details/cleanRoom/__tests__/cleanRoomLogic.test.ts">未密閉 %j は見出しだけを出す</web-test>
        [TestCaseSource(nameof(UnsealedCases))]
        public void 未密閉の部屋は見出しだけを出す(CleanRoomFetchedDetail cleanRoom, string headlineKey, string[] headlineParams, bool insufficient)
        {
            var view = CleanRoomLogic.Display(cleanRoom);
            UiTextAssert.AreEqual(headlineKey, headlineParams, view.Headline);
            Assert.AreEqual(insufficient, view.HeadlineInsufficient);
            Assert.IsInstanceOf<HeadlineOnlyCleanRoomDisplay>(view);
            Assert.IsTrue(view.TryGetHaltReason(out var haltReason));
            UiTextAssert.AreEqual(LocalizationKeys.Ui.BlockInventory.CleanRoomHaltNotEnclosed, haltReason);
        }
    }
}
