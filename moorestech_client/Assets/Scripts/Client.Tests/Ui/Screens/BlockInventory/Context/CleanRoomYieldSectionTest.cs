using System.Collections.Generic;
using Client.Game.InGame.BlockInfoPolling;
using Client.Tests.Ui.Screens.GrabOverlay;
using Client.Tests.Ui.State.BlockInventory.Context;
using Client.Tests.Ui.TestDoubles;
using Client.Tests.Ui.TestHost;
using Client.Ui.Foundation.Input.OsCursor;
using Client.Ui.Screens.BlockInventory.Context;
using Client.Ui.State.BlockInventory.Common;
using Client.Ui.State.BlockInventory.Logic.CleanRoom;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.BlockInventory.Context
{
    /// <summary>
    /// 確率の欄の行の並び
    /// Odds section row order
    /// </summary>
    public class CleanRoomYieldSectionTest : ContextSectionTestHost
    {
        private static readonly IReadOnlyList<CleanRoomOddsImprovement> NoImprovements = new List<CleanRoomOddsImprovement>();

        private readonly RecordingCleanRoomYieldImprovementCommands _improvementCommands = new();

        private CleanRoomYieldSectionView Render(SectionContent<CleanRoomYieldOdds> entry)
        {
            var view = new CleanRoomYieldSectionView(new UiOsCursorRequestStack(new FakeUiOsCursorApplier(), new FakeUiScreenPointer(1f)), _improvementCommands);
            view.Render(entry);
            Mount(view);
            return view;
        }

        [Test]
        public void EUVは失敗の1行だけを描く()
        {
            var view = Render(SectionContent<CleanRoomYieldOdds>.Present(CleanRoomYieldOdds.LayerExposure(0.308, NoImprovements)));

            CollectionAssert.AreEqual(new[] { "clean-room-yield-failed-rate" }, ShownNames(view.Q<VisualElement>("clean-room-yield-section")));
            Assert.AreEqual("Failed: 30.8%", view.Q<Label>("clean-room-yield-failed-rate").PlainText());
        }

        [Test]
        public void ダイシングは出力と0でないLvと失敗の行を描く()
        {
            var view = Render(SectionContent<CleanRoomYieldOdds>.Present(CleanRoomYieldOdds.Dicing(300, new[] { 0.423, 0.072, 0 }, 0.505, NoImprovements)));

            CollectionAssert.AreEqual(
                new[] { "clean-room-yield-output-count", "clean-room-yield-level-rate-1", "clean-room-yield-level-rate-2", "clean-room-yield-failed-rate" },
                ShownNames(view.Q<VisualElement>("clean-room-yield-section")));
            Assert.AreEqual("Output: 300", view.Q<Label>("clean-room-yield-output-count").PlainText());
            Assert.AreEqual("Lv2: 7.2%", view.Q<Label>("clean-room-yield-level-rate-2").PlainText());
            Assert.AreEqual("Failed: 50.5%", view.Q<Label>("clean-room-yield-failed-rate").PlainText());
        }

        [Test]
        public void 読めない応答の間は取得状態の1行だけを描き率を出さない()
        {
            var view = Render(SectionContent<CleanRoomYieldOdds>.Unavailable(BlockInfoFetchState.Unreadable));

            CollectionAssert.AreEqual(new[] { "clean-room-yield-fetch-state" }, ShownNames(view));
            Assert.AreEqual("Draw details: received data could not be read (retrying)", view.Q<Label>("clean-room-yield-fetch-state").PlainText());
        }

        [Test]
        public void 改善の行があればボタンを最後の行に置き_押すと改善方法を開く()
        {
            var improvements = new List<CleanRoomOddsImprovement> { CleanRoomOddsImprovement.RoomClassUp("B", new double[0], -0.1) };
            var view = Render(SectionContent<CleanRoomYieldOdds>.Present(CleanRoomYieldOdds.LayerExposure(0.308, improvements)));
            simulate.FrameUpdate();

            CollectionAssert.AreEqual(new[] { "clean-room-yield-failed-rate", "clean-room-yield-improvement-open" }, ShownNames(view.Q<VisualElement>("clean-room-yield-section")));
            Assert.AreEqual("How to lower the failure rate", view.Q<VisualElement>("clean-room-yield-improvement-open").Q<Label>().PlainText());
            simulate.Click(view.Q<VisualElement>("clean-room-yield-improvement-open").worldBound.center);
            Assert.AreEqual(1, _improvementCommands.OpenCount);
        }

        [Test]
        public void 改善の行が無くなるとボタンを外す()
        {
            var improvements = new List<CleanRoomOddsImprovement> { CleanRoomOddsImprovement.RoomClassUp("B", new double[0], -0.1) };
            var view = Render(SectionContent<CleanRoomYieldOdds>.Present(CleanRoomYieldOdds.LayerExposure(0.308, improvements)));
            view.Render(SectionContent<CleanRoomYieldOdds>.Present(CleanRoomYieldOdds.LayerExposure(0.308, NoImprovements)));
            simulate.FrameUpdate();

            CollectionAssert.AreEqual(new[] { "clean-room-yield-failed-rate" }, ShownNames(view.Q<VisualElement>("clean-room-yield-section")));
        }
    }
}
