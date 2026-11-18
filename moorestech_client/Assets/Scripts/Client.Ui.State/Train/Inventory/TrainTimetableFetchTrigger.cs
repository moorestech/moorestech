using Client.Ui.State.Train.Timetable;
using Client.Ui.State.Train.TimetableEdit.Send;

namespace Client.Ui.State.Train.Inventory
{
    /// <summary>
    /// 時刻表の取得を頼む契機を決める（Web TrainInventoryBody の effect の写し。重複の依頼は Fetcher が畳む）
    /// Decides when to ask for a timetable fetch (copy of the Web TrainInventoryBody effect; the Fetcher folds duplicates)
    /// </summary>
    public sealed class TrainTimetableFetchTrigger
    {
        private readonly ITrainTimetableCommands _timetableCommands;

        public TrainInventoryTab Tab { get; private set; } = TrainInventoryTab.Inventory;

        public TrainTimetableFetchTrigger(ITrainTimetableCommands timetableCommands)
        {
            _timetableCommands = timetableCommands;
        }

        // 時刻表タブへ移った瞬間だけ依頼する。用意済みでも頼み直す
        // Asks only on the switch into the timetable tab, even when already ready
        public void SelectTab(TrainInventoryTab tab)
        {
            var enteredTimetable = tab == TrainInventoryTab.Timetable && Tab != TrainInventoryTab.Timetable;
            Tab = tab;
            if (enteredTimetable) _timetableCommands.RequestOpen();
        }

        // 時刻表タブのあいだに読込中（取得のやり直し）が届いたら頼み直す
        // While on the timetable tab, a loading state (a reset fetch) asks again
        public void ReceiveState(TrainTimetableViewState state)
        {
            if (Tab == TrainInventoryTab.Timetable && state is LoadingTrainTimetableViewState) _timetableCommands.RequestOpen();
        }
    }
}
