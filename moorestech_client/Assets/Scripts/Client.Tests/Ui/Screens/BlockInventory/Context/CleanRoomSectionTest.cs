using Client.Game.InGame.BlockInfoPolling;
using Client.Ui.Screens.BlockInventory.Context;
using Client.Ui.State.BlockInventory.Context;
using Client.Ui.State.BlockInventory.Logic.CleanRoom;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.BlockInventory.Context
{
    /// <summary>
    /// 部屋欄の行の並びと赤の付き方（View を直に組み、状態を差して見る）
    /// Room section row order and red marking (builds the View directly and feeds it states)
    /// </summary>
    public class CleanRoomSectionTest : ContextSectionTestHost
    {
        private const string InsufficientClass = "lack-highlight-text--insufficient";

        private CleanRoomSectionView Render(ContextEntry<CleanRoomFetchedDetail> entry)
        {
            var view = new CleanRoomSectionView();
            view.Render(entry);
            Mount(view);
            return view;
        }

        /// <web-test file="src/features/blockInventory/details/cleanRoom/__tests__/CleanRoomSection.test.ts">密閉済みの部屋は見出し・空気・清浄機・停止理由の順に描く</web-test>
        [Test]
        public void 密閉済みの部屋は見出し空気清浄機停止理由の順()
        {
            var filters = new[] { new CleanRoomAirFilter(1, 2, 0.5), new CleanRoomAirFilter(0.5, 1, 0.25) };
            var summary = new CleanRoomSummary(CleanRoomHeadline.Class("ISO 5"), 100, 20, filters);
            var view = Render(ContextEntry<CleanRoomFetchedDetail>.Fetched(CleanRoomFetchedDetail.Enclosed(summary, CleanRoomHaltReason.ClassOut)));

            CollectionAssert.AreEqual(new[] { "clean-room-headline", "clean-room-air", "clean-room-air-filter-0", "clean-room-air-filter-1", "clean-room-halt-reason" }, ShownNames(view.Q<VisualElement>("clean-room-section")));
            Assert.IsFalse(view.Q<VisualElement>("clean-room-headline").ClassListContains(InsufficientClass));
            Assert.IsTrue(view.Q<VisualElement>("clean-room-halt-reason").ClassListContains(InsufficientClass));
        }

        /// <web-test file="src/features/blockInventory/details/cleanRoom/__tests__/CleanRoomSection.test.ts">清浄機が無い部屋はその旨を赤で出す</web-test>
        [Test]
        public void 清浄機が無い部屋はその旨を赤で出す()
        {
            var summary = new CleanRoomSummary(CleanRoomHeadline.Class("A"), 0, 0, new CleanRoomAirFilter[0]);
            var view = Render(ContextEntry<CleanRoomFetchedDetail>.Fetched(CleanRoomFetchedDetail.Enclosed(summary, CleanRoomHaltReason.None)));

            CollectionAssert.AreEqual(new[] { "clean-room-headline", "clean-room-air", "clean-room-no-filter" }, ShownNames(view.Q<VisualElement>("clean-room-section")));
            Assert.IsTrue(view.Q<VisualElement>("clean-room-no-filter").ClassListContains(InsufficientClass));
        }

        /// <web-test file="src/features/blockInventory/details/cleanRoom/__tests__/CleanRoomSection.test.ts">隙間がある部屋は件数の見出しだけを赤で出す</web-test>
        [Test]
        public void 隙間がある部屋は件数の見出しだけを赤で出す()
        {
            var view = Render(ContextEntry<CleanRoomFetchedDetail>.Fetched(CleanRoomFetchedDetail.Openings(2, CleanRoomHaltReason.None)));

            CollectionAssert.AreEqual(new[] { "clean-room-headline" }, ShownNames(view.Q<VisualElement>("clean-room-section")));
            var headline = view.Q<Label>("clean-room-headline");
            Assert.IsTrue(headline.ClassListContains(InsufficientClass));
            Assert.AreEqual("Not sealed: 2 gaps", headline.text);
        }

        /// <web-test file="src/features/blockInventory/details/cleanRoom/__tests__/CleanRoomSection.test.ts">取得できていない間は欄の名前と取得状態の1行だけを描き、部屋の値を出さない</web-test>
        [Test]
        public void 取得できていない間は欄の名前と取得状態の1行だけ()
        {
            var view = Render(ContextEntry<CleanRoomFetchedDetail>.Unavailable(BlockInfoFetchState.TransportFailed));

            CollectionAssert.AreEqual(new[] { "clean-room-fetch-state" }, ShownNames(view));
            Assert.IsNull(view.Q<VisualElement>("clean-room-section"));
            Assert.AreEqual("Room: no reply from the server (retrying)", view.Q<Label>("clean-room-fetch-state").text);
        }
    }
}
