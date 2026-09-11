using System.Collections.Generic;
using Game.Paths;

namespace Server.Boot.Replay
{
    // 再生の入力。どのワールド（マップ）のどのスナップショットから、どの区間ファイルを使ってどのtickまで進めるか
    // The replay input: which world (map), which snapshot to start from, which segment files to feed, and which tick to reach
    public sealed class ReplayRequest
    {
        public string ServerDataDirectory { get; }

        // 記録時と同じワールド。template ではなくバンドルに入っている世界を読むための出所
        // The same world as the recording; the source of the map so replay reads the bundled world, not the template
        public WorldDataDirectory SourceWorld { get; }
        public string SnapshotFilePath { get; }
        public IReadOnlyList<string> PacketLogFilePaths { get; }
        public ulong TargetTick { get; }

        public ReplayRequest(string serverDataDirectory, WorldDataDirectory sourceWorld, string snapshotFilePath, IReadOnlyList<string> packetLogFilePaths, ulong targetTick)
        {
            ServerDataDirectory = serverDataDirectory;
            SourceWorld = sourceWorld;
            SnapshotFilePath = snapshotFilePath;
            PacketLogFilePaths = packetLogFilePaths;
            TargetTick = targetTick;
        }
    }
}
