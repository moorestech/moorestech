using Client.Ui.State.Routing;

namespace Client.Ui.State.Train
{
    /// <summary>
    /// 列車乗車 HUD の出し分けと分岐の表示（Web の screenShowsTrainHud ∧ train.riding の riding・branchCandidateCount）
    /// Visibility and branch display of the train riding HUD (Web screenShowsTrainHud and train.riding riding, branchCandidateCount)
    /// </summary>
    public sealed class TrainRidingHudViewModel
    {
        public bool Shown { get; }
        public bool BranchShown { get; }
        public int SelectedBranchNumber { get; }
        public int BranchCandidateCount { get; }

        private TrainRidingHudViewModel(bool shown, bool branchShown, int selectedBranchNumber, int branchCandidateCount)
        {
            Shown = shown;
            BranchShown = branchShown;
            SelectedBranchNumber = selectedBranchNumber;
            BranchCandidateCount = branchCandidateCount;
        }

        // 画面の traits と、列車 HUD 画面上で乗車中か（TrainHUDScreenState.IsRidingOnScreen）の両方で出す
        // Shown when both the screen traits allow it and the player is riding on the train HUD screen (IsRidingOnScreen)
        public static TrainRidingHudViewModel Create(UiScreen screen, bool isRidingOnScreen, int branchCandidateCount, int selectedBranchIndex)
        {
            var shown = UiScreenTraitsTable.Get(screen).TrainHud && isRidingOnScreen;
            return new TrainRidingHudViewModel(shown, shown && 1 < branchCandidateCount, selectedBranchIndex + 1, branchCandidateCount);
        }
    }
}
