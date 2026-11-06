using System.Collections.Generic;
using Client.Tests.Ui.TestDoubles;
using Client.Ui.State.BlockInventory.Logic.CleanRoom;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;

namespace Client.Tests.Ui.State.BlockInventory.CleanRoom
{
    /// <summary>
    /// Web の cleanRoomLogic.test.ts の写し（L0）
    /// Port of Web cleanRoomLogic.test.ts (L0)
    /// </summary>
    public class CleanRoomLogicTest
    {
        /// <web-test file="src/features/blockInventory/details/cleanRoom/__tests__/cleanRoomLogic.test.ts">クラスに届いた部屋はクラス名・濃度・換気回数・清浄機ごとの行を出す</web-test>
        [Test]
        public void クラスに届いた部屋はクラス名と濃度と換気回数と清浄機ごとの行を出す()
        {
            var room = new CleanRoomSummary(CleanRoomHeadline.Class("A"), 0.12345, 2.5, new[] { new CleanRoomAirFilter(0.756, 5, 0.5) });
            var view = CleanRoomLogic.Display(CleanRoomFetchedDetail.Enclosed(room, CleanRoomHaltReason.None));
            UiTextAssert.AreEqual(LocalizationKeys.Ui.BlockInventory.CleanRoomClass, new[] { "className", "A" }, view.Headline);
            Assert.IsFalse(view.HeadlineInsufficient);
            Assert.IsInstanceOf<EnclosedCleanRoomDisplay>(view);
            var enclosed = (EnclosedCleanRoomDisplay)view;
            UiTextAssert.AreEqual(LocalizationKeys.Ui.BlockInventory.CleanRoomAir, new[] { "concentration", "0.123", "airChangeRate", "2.500" }, enclosed.Air);
            Assert.AreEqual(1, enclosed.Filters.Count);
            UiTextAssert.AreEqual(LocalizationKeys.Ui.BlockInventory.CleanRoomFilter, new[] { "index", "1", "power", "76", "count", "5", "remaining", "50" }, enclosed.Filters[0]);
            Assert.IsFalse(enclosed.ShowsNoFilter);
            Assert.IsFalse(view.TryGetHaltReason(out _));
        }

        /// <web-test file="src/features/blockInventory/details/cleanRoom/__tests__/cleanRoomLogic.test.ts">Outの部屋は赤で示し、清浄機が無ければその旨を出す</web-test>
        [Test]
        public void Outの部屋は赤で示し清浄機が無ければその旨を出す()
        {
            var outRoom = new CleanRoomSummary(CleanRoomHeadline.ClassOut, 3, 0, new CleanRoomAirFilter[0]);
            var view = CleanRoomLogic.Display(CleanRoomFetchedDetail.Enclosed(outRoom, CleanRoomHaltReason.ClassOut));
            UiTextAssert.AreEqual(LocalizationKeys.Ui.BlockInventory.CleanRoomClassOut, view.Headline);
            Assert.IsTrue(view.HeadlineInsufficient);
            var enclosed = (EnclosedCleanRoomDisplay)view;
            Assert.IsTrue(enclosed.ShowsNoFilter);
            UiTextAssert.AreEqual(LocalizationKeys.Ui.BlockInventory.CleanRoomNoFilter, enclosed.NoFilter);
            Assert.IsTrue(view.TryGetHaltReason(out var haltReason));
            UiTextAssert.AreEqual(LocalizationKeys.Ui.BlockInventory.CleanRoomHaltClassOut, haltReason);
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
