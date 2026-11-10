namespace Client.Ui.State.Train.Platform.Edit
{
    /// <summary>
    /// 駅名の下書き。同期済みの名前と同じ入力（未編集）だけが同期に従い、拒否で最新の同期済みへ戻す（Web TrainStationNameSection の写し）
    /// Station name draft: only an input equal to the synced name (unedited) follows syncs; rejection reverts to the latest synced name (copy of Web TrainStationNameSection)
    /// </summary>
    public sealed class TrainStationNameDraft
    {
        private string _synced = string.Empty;

        public string Input { get; private set; } = string.Empty;

        public void Sync(string syncedName)
        {
            // Web と同じく「前の同期値と同じ入力」を未編集とみなす
            // Same as Web: an input equal to the previous synced value counts as unedited
            if (Input == _synced) Input = syncedName;
            _synced = syncedName;
        }

        public void Edit(string input)
        {
            Input = input;
        }

        // 非受理・到達不能なら入力を捨て最新の同期値へ戻す
        // On refusal or no reply, drop input and return to latest synced name
        public void Reject()
        {
            Input = _synced;
        }
    }
}
