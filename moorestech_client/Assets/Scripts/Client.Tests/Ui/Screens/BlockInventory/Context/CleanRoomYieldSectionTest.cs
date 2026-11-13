using Client.Game.InGame.BlockInfoPolling;
using Client.Ui.Screens.BlockInventory.Context;
using Client.Ui.State.BlockInventory.Common;
using Client.Ui.State.BlockInventory.Context;
using Client.Ui.State.BlockInventory.Logic.CleanRoom;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.BlockInventory.Context
{
    /// <summary>
    /// 抽選の中身欄の行の並び
    /// Draw-detail section row order
    /// </summary>
    public class CleanRoomYieldSectionTest : ContextSectionTestHost
    {
        private CleanRoomYieldSectionView Render(SectionContent<CleanRoomYieldFetched> entry)
        {
            var view = new CleanRoomYieldSectionView();
            view.Render(entry);
            Mount(view);
            return view;
        }

        /// <web-test file="src/features/blockInventory/details/cleanRoom/__tests__/CleanRoomYieldSection.test.ts">EUV は層の失敗率の行だけを描く</web-test>
        [Test]
        public void EUVは層の失敗率の行だけを描く()
        {
            var rates = CleanRoomYieldRates.LayerExposure(0.02);
            var view = Render(SectionContent<CleanRoomYieldFetched>.Present(CleanRoomYieldFetched.WithoutPendingDraw(rates)));

            CollectionAssert.AreEqual(new[] { "clean-room-yield-layer-fail-rate" }, ShownNames(view.Q<VisualElement>("clean-room-yield-section")));
        }

        /// <web-test file="src/features/blockInventory/details/cleanRoom/__tests__/CleanRoomYieldSection.test.ts">ダイシングは品質Tier・損失率・0でないレベル割合・失敗割合の行を描く</web-test>
        [Test]
        public void ダイシングは品質Tier損失率0でないレベル割合失敗割合の行を描く()
        {
            var rates = CleanRoomYieldRates.Dicing(0.03, 1, new[] { 0.7, 0.1, 0 }, 0.2);
            var view = Render(SectionContent<CleanRoomYieldFetched>.Present(CleanRoomYieldFetched.WithoutPendingDraw(rates)));

            CollectionAssert.AreEqual(
                new[] { "clean-room-yield-quality-tier", "clean-room-yield-die-loss-rate", "clean-room-yield-level-rate-1", "clean-room-yield-level-rate-2", "clean-room-yield-failed-rate" },
                ShownNames(view.Q<VisualElement>("clean-room-yield-section")));
            Assert.AreEqual("Quality module tier 1", view.Q<Label>("clean-room-yield-quality-tier").text);
            Assert.AreEqual("Lv2 share 10.0%", view.Q<Label>("clean-room-yield-level-rate-2").text);
        }

        /// <web-test file="src/features/blockInventory/details/cleanRoom/__tests__/CleanRoomYieldSection.test.ts">読めない応答の間は取得状態の1行だけを描き、率を出さない</web-test>
        [Test]
        public void 読めない応答の間は取得状態の1行だけを描き率を出さない()
        {
            var view = Render(SectionContent<CleanRoomYieldFetched>.Unavailable(BlockInfoFetchState.Unreadable));

            CollectionAssert.AreEqual(new[] { "clean-room-yield-fetch-state" }, ShownNames(view));
            Assert.AreEqual("Draw details: received data could not be read (retrying)", view.Q<Label>("clean-room-yield-fetch-state").text);
        }
    }
}
