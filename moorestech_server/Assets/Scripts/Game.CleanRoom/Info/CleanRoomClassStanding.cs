namespace Game.CleanRoom.Info
{
    /// <summary>
    ///     部屋のクラスの立ち位置。Out か、閾値行に入った分類済みかのどちらか1つだけになる
    ///     A room's class standing; always exactly one of Out or classified into a threshold row
    /// </summary>
    public abstract class CleanRoomClassStanding
    {
        // 種別はこのファイルの2つの型に閉じる
        // The kinds are closed to the two types in this file
        private protected CleanRoomClassStanding()
        {
        }

        public static CleanRoomClassStanding Of(CleanRoom room)
        {
            // Out の判定は部屋の TryGetClassRow だけに任せる
            // Leave the Out decision solely to the room's TryGetClassRow
            if (!room.TryGetClassRow(out var row)) return CleanRoomOutStanding.Instance;
            return new CleanRoomClassifiedStanding(row.ClassName, room.ThresholdIndex);
        }
    }

    public sealed class CleanRoomOutStanding : CleanRoomClassStanding
    {
        public static readonly CleanRoomOutStanding Instance = new CleanRoomOutStanding();

        private CleanRoomOutStanding()
        {
        }
    }

    public sealed class CleanRoomClassifiedStanding : CleanRoomClassStanding
    {
        public string ClassName { get; }
        public int ThresholdIndex { get; }

        public CleanRoomClassifiedStanding(string className, int thresholdIndex)
        {
            ClassName = className;
            ThresholdIndex = thresholdIndex;
        }
    }
}
