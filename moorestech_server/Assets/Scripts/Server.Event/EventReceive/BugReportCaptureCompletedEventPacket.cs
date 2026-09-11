using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Context;
using Game.Paths;
using Game.SaveLoad.Interface;
using MessagePack;
using UniRx;

namespace Server.Event.EventReceive
{
    // 即時スナップショットの書き出し完了を、バンドル組み立てに必要なファイル一覧付きで配信する
    // Broadcasts that an immediate snapshot finished writing, with the file list needed to assemble a bundle
    public class BugReportCaptureCompletedEventPacket : IBootInitializable
    {
        public const string EventTag = "va:event:bugReportCaptureCompleted";

        private readonly EventProtocolProvider _eventProtocolProvider;
        private readonly ISnapshotWrittenNotifier _snapshotWrittenNotifier;
        private readonly WorldDataDirectory _worldDataDirectory;

        public BugReportCaptureCompletedEventPacket(EventProtocolProvider eventProtocolProvider, ISnapshotWrittenNotifier snapshotWrittenNotifier, WorldDataDirectory worldDataDirectory)
        {
            _eventProtocolProvider = eventProtocolProvider;
            _snapshotWrittenNotifier = snapshotWrittenNotifier;
            _worldDataDirectory = worldDataDirectory;
        }

        public void Load()
        {
            // 周期スナップショット（RequestId=0）は配信しない。要求付きの完了だけを流す
            // Periodic snapshots (RequestId=0) are not broadcast; only requested completions are
            _snapshotWrittenNotifier.OnSnapshotWritten.Where(w => w.RequestId != 0).Subscribe(OnSnapshotWritten);

            #region Internal

            void OnSnapshotWritten(SnapshotWritten written)
            {
                var directory = _worldDataDirectory.SnapshotDirectory;
                var snapshots = Directory.GetFiles(directory, "tick_*.json").Select(Path.GetFileName).OrderBy(n => n).ToList();
                var packetLogs = Directory.GetFiles(directory, "packets_*.bin").Select(Path.GetFileName).OrderBy(n => n).ToList();
                var payload = MessagePackSerializer.Serialize(new BugReportCaptureCompletedMessagePack(written.RequestId, written.Tick, directory, snapshots, packetLogs));
                _eventProtocolProvider.AddBroadcastEvent(EventTag, payload);
            }

            #endregion
        }

        [MessagePackObject]
        public class BugReportCaptureCompletedMessagePack
        {
            [Key(0)] public long CaptureId { get; set; }
            [Key(1)] public ulong Tick { get; set; }
            [Key(2)] public string SnapshotDirectory { get; set; }
            [Key(3)] public List<string> SnapshotFileNames { get; set; }
            [Key(4)] public List<string> PacketLogFileNames { get; set; }

            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public BugReportCaptureCompletedMessagePack() { }

            public BugReportCaptureCompletedMessagePack(long captureId, ulong tick, string snapshotDirectory, List<string> snapshotFileNames, List<string> packetLogFileNames)
            {
                CaptureId = captureId;
                Tick = tick;
                SnapshotDirectory = snapshotDirectory;
                SnapshotFileNames = snapshotFileNames;
                PacketLogFileNames = packetLogFileNames;
            }
        }
    }
}
