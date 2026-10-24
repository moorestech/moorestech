using System;
using System.Collections.Generic;

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
        // 1件以上（共有壁なら複数）
        // One or more (several for a shared wall)
        public IReadOnlyList<CleanRoomSummaryMessagePack> Rooms { get; }

        public CleanRoomEnclosedView(IReadOnlyList<CleanRoomSummaryMessagePack> rooms)
        {
            if (rooms.Count == 0) throw new ArgumentException("密閉済みの読み口は部屋を1件以上持つ / An enclosed view holds at least one room", nameof(rooms));
            Rooms = rooms;
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
