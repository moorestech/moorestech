
namespace Server.Protocol.PacketResponse
{
    /// <summary>
    ///     漏れの応答を種別ごとの型で読む口。漏れセルと総数は Openings、上限体積は TooLarge の型だけが持つ
    ///     Per-kind read view of the leak response; only the openings type has cells and a total, only the too-large type has the volume cap
    /// </summary>
    public abstract class CleanRoomLeakView
    {
        // 種別はこのファイルの4つの型に閉じる
        // The kinds are closed to the four types in this file
        private protected CleanRoomLeakView()
        {
        }
    }

    public sealed class CleanRoomOpeningsLeakView : CleanRoomLeakView
    {
        public int TotalOpeningCount { get; }

        public CleanRoomOpeningsLeakView(int totalOpeningCount)
        {
            TotalOpeningCount = totalOpeningCount;
        }
    }

    public sealed class CleanRoomNoEnclosureLeakView : CleanRoomLeakView
    {
        public static readonly CleanRoomNoEnclosureLeakView Instance = new CleanRoomNoEnclosureLeakView();

        private CleanRoomNoEnclosureLeakView()
        {
        }
    }

    public sealed class CleanRoomTooLargeLeakView : CleanRoomLeakView
    {
        // TooLarge の文言に出す部屋の上限体積
        // Room volume cap shown in the TooLarge message
        public int MaxRoomVolume { get; }

        public CleanRoomTooLargeLeakView(int maxRoomVolume)
        {
            MaxRoomVolume = maxRoomVolume;
        }
    }

    public sealed class CleanRoomDetectionPendingLeakView : CleanRoomLeakView
    {
        public static readonly CleanRoomDetectionPendingLeakView Instance = new CleanRoomDetectionPendingLeakView();

        private CleanRoomDetectionPendingLeakView()
        {
        }
    }
}
