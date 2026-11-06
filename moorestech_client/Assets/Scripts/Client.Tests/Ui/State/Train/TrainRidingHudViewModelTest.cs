using System;
using Client.Game.InGame.UI.UIState;
using Client.Ui.State.Routing;
using Client.Ui.State.Train;
using NUnit.Framework;

namespace Client.Tests.Ui.State.Train
{
    /// <summary>
    /// 乗車 HUD が traits の TrainHud 列の画面かつ列車 HUD の state で乗車中の間だけ出て、分岐は 2 以上で出ることを確かめる（L1）
    /// Verifies the riding HUD shows only on screens with the traits TrainHud column while in the train HUD state and riding, with branches shown from two (L1)
    /// </summary>
    public class TrainRidingHudViewModelTest
    {
        [Test]
        public void 乗車HUDはtraitsのTrainHud列と乗車中でだけ出て分岐は2以上で出る()
        {
            // 全画面の行で出し分けが TrainHud 列と一致する
            // On every screen row the visibility matches the TrainHud column
            foreach (UiScreen screen in Enum.GetValues(typeof(UiScreen)))
            {
                var model = TrainRidingHudViewModel.Create(screen, UIStateEnum.TrainHUDScreen, true, 1, 0);
                Assert.AreEqual(UiScreenTraitsTable.Get(screen).TrainHud, model.Shown, screen.ToString());
            }

            // 乗っていない・列車 HUD の state でないなら出さない
            // Hidden when not riding or outside the train HUD state
            Assert.IsFalse(TrainRidingHudViewModel.Create(UiScreen.TrainHud, UIStateEnum.TrainHUDScreen, false, 1, 0).Shown);
            Assert.IsFalse(TrainRidingHudViewModel.Create(UiScreen.TrainHud, UIStateEnum.GameScreen, true, 1, 0).Shown);

            // 分岐は候補 2 以上で出し、番号は 1 始まり
            // Branches show from two candidates, numbered from one
            var single = TrainRidingHudViewModel.Create(UiScreen.TrainHud, UIStateEnum.TrainHUDScreen, true, 1, 0);
            var branched = TrainRidingHudViewModel.Create(UiScreen.TrainHud, UIStateEnum.TrainHUDScreen, true, 3, 1);
            Assert.IsFalse(single.BranchShown);
            Assert.IsTrue(branched.BranchShown);
            Assert.AreEqual(2, branched.SelectedBranchNumber);
            Assert.AreEqual(3, branched.BranchCandidateCount);

            // しきい値ちょうどの候補 2 でも出す
            // Shows at exactly two candidates, the threshold
            var twoWay = TrainRidingHudViewModel.Create(UiScreen.TrainHud, UIStateEnum.TrainHUDScreen, true, 2, 0);
            Assert.IsTrue(twoWay.BranchShown);
            Assert.AreEqual(1, twoWay.SelectedBranchNumber);
            Assert.AreEqual(2, twoWay.BranchCandidateCount);
        }
    }
}
