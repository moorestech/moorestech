
namespace Game.CleanRoom.Info
{
    /// <summary>
    ///     ブロック1つから見た部屋の状態。密閉・未密閉の種別ごとの型のどちらか1つだけになる
    ///     Room state seen from one block; always exactly one of the per-kind types, enclosed or not enclosed
    /// </summary>
    public abstract class CleanRoomInfo
    {
        // 種別はこのファイルの2つの型に閉じる
        // The kinds are closed to the two types in this file
        private protected CleanRoomInfo()
        {
        }
    }

    /// <summary>
    ///     密閉済み。ブロックが属する部屋の要約
    ///     Enclosed: summary of the room the block belongs to
    /// </summary>
    public sealed class CleanRoomEnclosedInfo : CleanRoomInfo
    {
        public CleanRoomRoomSummary Room { get; }

        public CleanRoomEnclosedInfo(CleanRoomRoomSummary room)
        {
            Room = room;
        }
    }

    /// <summary>
    ///     未密閉。密閉できていない理由と漏れ箇所
    ///     Not enclosed: why it is not sealed and where it leaks
    /// </summary>
    public sealed class CleanRoomNotEnclosedInfo : CleanRoomInfo
    {
        public CleanRoomLeakReport Leak { get; }

        public CleanRoomNotEnclosedInfo(CleanRoomLeakReport leak)
        {
            Leak = leak;
        }
    }
}
