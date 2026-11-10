using Client.Ui.State.Train.Timetable;

namespace Client.Ui.State.Train.Inventory
{
    /// <summary>
    /// 時刻表の取得を頼む契機を決める（Web TrainInventoryBody の effect の写し。重複の依頼は Fetcher が畳む）
    /// Decides when to ask for a timetable fetch (copy of the Web TrainInventoryBody effect; the Fetcher folds duplicates)
    /// </summary>
    public sealed class TrainTimetableFetchTrigger
    {
        public TrainInventoryTab Tab { get; private set; } = TrainInventoryTab.Inventory;

        // 時刻表タブへ移った瞬間だけ真。用意済みでも頼み直す
        // True only on the switch into the timetable tab, even when already ready
        public bool SelectTab(TrainInventoryTab tab)
        {
            var enteredTimetable = tab == TrainInventoryTab.Timetable && Tab != TrainInventoryTab.Timetable;
            Tab = tab;
            return enteredTimetable;
        }

        // 時刻表タブのあいだに読込中（取得のやり直し）が届いたら頼み直す
        // While on the timetable tab, a loading state (a reset fetch) asks again
        public bool ReceiveState(TrainTimetableViewState state)
        {
            return Tab == TrainInventoryTab.Timetable && state is LoadingTrainTimetableViewState;
        }
    }
}
