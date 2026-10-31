namespace Server.Protocol.PacketResponse
{
    /// <summary>
    ///     部屋照会の応答を種別ごとの型で読む口。BlockNotFound はこの型に入らず、取得側（ポーラー）が扱う
    ///     Per-kind read view of the room query response; BlockNotFound is not part of it and is handled by the fetcher (poller)
    /// </summary>
    public abstract class CleanRoomInfoView
    {
        // 種別はこのファイルの2つの型に閉じる
        // The kinds are closed to the two types in this file
        private protected CleanRoomInfoView()
        {
        }
    }

    public sealed class CleanRoomEnclosedView : CleanRoomInfoView
    {
        public CleanRoomSummaryMessagePack Room { get; }

        public CleanRoomEnclosedView(CleanRoomSummaryMessagePack room)
        {
            Room = room;
        }
    }

    public sealed class CleanRoomNotEnclosedView : CleanRoomInfoView
    {
        public CleanRoomLeakView Leak { get; }

        public CleanRoomNotEnclosedView(CleanRoomLeakView leak)
        {
            Leak = leak;
        }
    }
}
