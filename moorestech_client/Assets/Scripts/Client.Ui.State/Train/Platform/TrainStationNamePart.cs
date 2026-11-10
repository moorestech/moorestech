namespace Client.Ui.State.Train.Platform
{
    /// <summary>
    /// 駅だけが持つ部分。サーバーと同期した駅名（届く前は空）
    /// Part only a station has: the station name synced from the server (empty until it arrives)
    /// </summary>
    public sealed class TrainStationNamePart
    {
        public string SyncedName { get; }

        public TrainStationNamePart(string syncedName)
        {
            SyncedName = syncedName;
        }
    }
}
