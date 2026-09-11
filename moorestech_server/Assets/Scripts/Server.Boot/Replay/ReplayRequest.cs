using System.Collections.Generic;

namespace Server.Boot.Replay
{
    // 再生の入力。どのスナップショットからどの区間ファイルを使ってどのtickまで進めるか
    // The replay input: which snapshot to start from, which segment files to feed, and which tick to reach
    public sealed class ReplayRequest
    {
        public string ServerDataDirectory { get; }
        public string SnapshotFilePath { get; }
        public IReadOnlyList<string> PacketLogFilePaths { get; }
        public ulong TargetTick { get; }

        public ReplayRequest(string serverDataDirectory, string snapshotFilePath, IReadOnlyList<string> packetLogFilePaths, ulong targetTick)
        {
            ServerDataDirectory = serverDataDirectory;
            SnapshotFilePath = snapshotFilePath;
            PacketLogFilePaths = packetLogFilePaths;
            TargetTick = targetTick;
        }
    }
}
