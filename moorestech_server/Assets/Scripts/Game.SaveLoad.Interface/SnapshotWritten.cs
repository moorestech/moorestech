using System.Collections.Generic;

namespace Game.SaveLoad.Interface
{
    // スナップショット1件の書き出し結果と、その時点でリングが権威として持つバンドル用ファイル一覧
    // The outcome of one snapshot write plus the bundle file list the ring authoritatively holds at that moment
    public sealed class SnapshotWritten
    {
        // 要求元の有無。周期スナップショットは要求元を持たず RequestId も意味を持たない
        // Whether a requester exists; a periodic snapshot has none and its RequestId carries no meaning
        public bool HasRequester { get; }
        public long RequestId { get; }
        public ulong Tick { get; }

        // スナップショット書き出しの成否。パケット記録の健全性はこれとは別に PacketLogDegradeReason が表す
        // Whether the snapshot write succeeded; the packet capture's health is carried separately by PacketLogDegradeReason
        public bool Success { get; }
        public string SnapshotDirectory { get; }
        public IReadOnlyList<string> SnapshotFileNames { get; }
        public IReadOnlyList<string> PacketLogFileNames { get; }

        // パケット記録が縮退した理由。空なら区間のパケットは欠けていない
        // Why packet capture degraded; empty means no packet is missing from the segments
        public string PacketLogDegradeReason { get; }

        // パケット記録を止めたtick。縮退していなければ0
        // The tick packet capture stopped at; 0 while healthy
        public ulong PacketLogDegradedAtTick { get; }

        private SnapshotWritten(bool hasRequester, long requestId, ulong tick, bool success,
            string snapshotDirectory, IReadOnlyList<string> snapshotFileNames, IReadOnlyList<string> packetLogFileNames,
            string packetLogDegradeReason, ulong packetLogDegradedAtTick)
        {
            HasRequester = hasRequester;
            RequestId = requestId;
            Tick = tick;
            Success = success;
            SnapshotDirectory = snapshotDirectory;
            SnapshotFileNames = snapshotFileNames;
            PacketLogFileNames = packetLogFileNames;
            PacketLogDegradeReason = packetLogDegradeReason;
            PacketLogDegradedAtTick = packetLogDegradedAtTick;
        }

        public static SnapshotWritten ForRequest(long requestId, ulong tick, bool success,
            string snapshotDirectory, IReadOnlyList<string> snapshotFileNames, IReadOnlyList<string> packetLogFileNames,
            string packetLogDegradeReason, ulong packetLogDegradedAtTick)
        {
            return new SnapshotWritten(true, requestId, tick, success, snapshotDirectory, snapshotFileNames, packetLogFileNames, packetLogDegradeReason, packetLogDegradedAtTick);
        }

        public static SnapshotWritten ForPeriodic(ulong tick, bool success,
            string snapshotDirectory, IReadOnlyList<string> snapshotFileNames, IReadOnlyList<string> packetLogFileNames,
            string packetLogDegradeReason, ulong packetLogDegradedAtTick)
        {
            return new SnapshotWritten(false, 0, tick, success, snapshotDirectory, snapshotFileNames, packetLogFileNames, packetLogDegradeReason, packetLogDegradedAtTick);
        }
    }
}
