using Client.Game.InGame.BlockInfoPolling;
using Client.Tests.Ui.TestHost;
using Client.Ui.Screens.BlockInventory.Context;
using Client.Ui.State.BlockInventory.Common;
using Client.Ui.State.BlockInventory.Logic.CleanRoom;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.BlockInventory.Context
{
    /// <summary>
    /// 部屋欄の行の並びと赤の付き方
    /// Room section row order and red marking
    /// </summary>
    public class CleanRoomSectionTest : ContextSectionTestHost
    {
        private const string InsufficientClass = "lack-highlight-text--insufficient";

        private CleanRoomSectionView Render(SectionContent<CleanRoomFetchedDetail> entry)
        {
            var view = new CleanRoomSectionView();
            view.Render(entry);
            Mount(view);
            return view;
        }

        [Test]
        public void 室内機械の部屋は部屋クラスと停止理由だけを描く()
        {
            var view = Render(SectionContent<CleanRoomFetchedDetail>.Present(CleanRoomFetchedDetail.Enclosed(CleanRoomSummary.ForMachine(CleanRoomHeadline.Class("C")), CleanRoomHaltReason.ClassOut)));

            CollectionAssert.AreEqual(new[] { "clean-room-headline", "clean-room-halt-reason" }, ShownNames(view.Q<VisualElement>("clean-room-section")));
            Assert.AreEqual("Room class C", view.Q<Label>("clean-room-headline").PlainText());
            Assert.IsFalse(view.Q<VisualElement>("clean-room-headline").ClassListContains(InsufficientClass));
            Assert.IsTrue(view.Q<VisualElement>("clean-room-halt-reason").ClassListContains(InsufficientClass));
        }

        [Test]
        public void 清浄機の部屋は台数つきの見出しと台数とこの清浄機の順に描き止まった台数は赤にしない()
        {
            var status = CleanRoomAirFilterRoomStatus.WithUnitsToNextClass(6, 1, 1, 0.5, "B", 5);
            var view = Render(SectionContent<CleanRoomFetchedDetail>.Present(CleanRoomFetchedDetail.Enclosed(CleanRoomSummary.ForAirFilter(CleanRoomHeadline.Class("C"), status), CleanRoomHaltReason.None)));

            CollectionAssert.AreEqual(new[] { "clean-room-headline", "clean-room-air-filter-count", "clean-room-queried-air-filter" }, ShownNames(view.Q<VisualElement>("clean-room-section")));
            Assert.AreEqual("Class C (5 more to class B)", view.Q<Label>("clean-room-headline").PlainText());
            Assert.AreEqual("Purifiers: 6 (1 stopped)", view.Q<Label>("clean-room-air-filter-count").PlainText());
            Assert.AreEqual("This purifier: power 100% / life 50%", view.Q<Label>("clean-room-queried-air-filter").PlainText());
            Assert.IsFalse(view.Q<VisualElement>("clean-room-air-filter-count").ClassListContains(InsufficientClass));
            Assert.AreEqual(PickingMode.Ignore, view.Q<Label>("clean-room-air-filter-count").pickingMode);
            Assert.AreEqual(PickingMode.Ignore, view.Q<Label>("clean-room-queried-air-filter").pickingMode);
        }

        [Test]
        public void 室内機械から清浄機の部屋へ替わると台数とこの清浄機の行が足され_戻すと消える()
        {
            var view = Render(SectionContent<CleanRoomFetchedDetail>.Present(CleanRoomFetchedDetail.Enclosed(CleanRoomSummary.ForMachine(CleanRoomHeadline.Class("C")), CleanRoomHaltReason.None)));
            var status = CleanRoomAirFilterRoomStatus.WithoutUnitsToNextClass(2, 0, 1, 1);
            view.Render(SectionContent<CleanRoomFetchedDetail>.Present(CleanRoomFetchedDetail.Enclosed(CleanRoomSummary.ForAirFilter(CleanRoomHeadline.Class("C"), status), CleanRoomHaltReason.None)));
            simulate.FrameUpdate();
            CollectionAssert.AreEqual(new[] { "clean-room-headline", "clean-room-air-filter-count", "clean-room-queried-air-filter" }, ShownNames(view.Q<VisualElement>("clean-room-section")));

            view.Render(SectionContent<CleanRoomFetchedDetail>.Present(CleanRoomFetchedDetail.Enclosed(CleanRoomSummary.ForMachine(CleanRoomHeadline.Class("C")), CleanRoomHaltReason.None)));
            simulate.FrameUpdate();
            CollectionAssert.AreEqual(new[] { "clean-room-headline" }, ShownNames(view.Q<VisualElement>("clean-room-section")));
        }

        /// <web-test file="src/features/blockInventory/details/cleanRoom/__tests__/CleanRoomSection.test.ts">隙間がある部屋は件数の見出しだけを赤で出す</web-test>
        [Test]
        public void 隙間がある部屋は件数の見出しだけを赤で出す()
        {
            var view = Render(SectionContent<CleanRoomFetchedDetail>.Present(CleanRoomFetchedDetail.Openings(2, CleanRoomHaltReason.None)));

            CollectionAssert.AreEqual(new[] { "clean-room-headline" }, ShownNames(view.Q<VisualElement>("clean-room-section")));
            var headline = view.Q<Label>("clean-room-headline");
            Assert.IsTrue(headline.ClassListContains(InsufficientClass));
            Assert.AreEqual("Not sealed: 2 gaps", headline.PlainText());
        }

        /// <web-test file="src/features/blockInventory/details/cleanRoom/__tests__/CleanRoomSection.test.ts">取得できていない間は欄の名前と取得状態の1行だけを描き、部屋の値を出さない</web-test>
        [Test]
        public void 取得できていない間は欄の名前と取得状態の1行だけ()
        {
            var view = Render(SectionContent<CleanRoomFetchedDetail>.Unavailable(BlockInfoFetchState.TransportFailed));

            CollectionAssert.AreEqual(new[] { "clean-room-fetch-state" }, ShownNames(view));
            Assert.IsNull(view.Q<VisualElement>("clean-room-section"));
            Assert.AreEqual("Room: no reply from the server (retrying)", view.Q<Label>("clean-room-fetch-state").PlainText());
        }
    }
}
