using System;
using System.Collections.Generic;

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
    ///     密閉済み。ブロックに接する部屋の要約（1件以上、共有壁なら複数）
    ///     Enclosed: summaries of the rooms touching the block (one or more, several for a shared wall)
    /// </summary>
    public sealed class CleanRoomEnclosedInfo : CleanRoomInfo
    {
        public IReadOnlyList<CleanRoomRoomSummary> Rooms { get; }

        public CleanRoomEnclosedInfo(IReadOnlyList<CleanRoomRoomSummary> rooms)
        {
            // 「1件以上」は照会の分岐だけに任せず、型の側で閉じる
            // Close "one or more" in the type itself instead of trusting only the query's branch
            if (rooms.Count == 0) throw new ArgumentException("密閉済みの照会結果は部屋を1件以上持つ / An enclosed result holds at least one room", nameof(rooms));
            Rooms = rooms;
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
