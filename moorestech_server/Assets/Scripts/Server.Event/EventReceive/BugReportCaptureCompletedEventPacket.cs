using System;
using System.Collections.Generic;
using System.Linq;
using Game.Context;
using Game.Paths;
using Game.SaveLoad.Interface;
using MessagePack;
using UniRx;

namespace Server.Event.EventReceive
{
    // 即時スナップショットの書き出し結果を、バンドル組み立てに必要なファイル一覧付きで要求元へ配信する
    // Delivers the outcome of an immediate snapshot write to its requester, with the file list needed to assemble a bundle
    public class BugReportCaptureCompletedEventPacket : IBootInitializable
    {
        public const string EventTag = "va:event:bugReportCaptureCompleted";

        private readonly EventProtocolProvider _eventProtocolProvider;
        private readonly ISnapshotWrittenNotifier _snapshotWrittenNotifier;
        private readonly BugReportCaptureRequesterRegistry _requesterRegistry;

        // 記録を再現するには、そのとき実際にマスタを読んだ置き場が要る。報告側で推測すると別のデータで再現してしまう
        // Reproducing a record needs the very directory the masters were read from; guessing it on the report side reproduces a different world
        private readonly ServerDataDirectory _serverDataDirectory;

        public BugReportCaptureCompletedEventPacket(EventProtocolProvider eventProtocolProvider, ISnapshotWrittenNotifier snapshotWrittenNotifier, BugReportCaptureRequesterRegistry requesterRegistry, ServerDataDirectory serverDataDirectory)
        {
            _eventProtocolProvider = eventProtocolProvider;
            _snapshotWrittenNotifier = snapshotWrittenNotifier;
            _requesterRegistry = requesterRegistry;
            _serverDataDirectory = serverDataDirectory;
        }

        public void Load()
        {
            // 要求元のいない周期スナップショットは配信しない。要求付きの結果だけを流す
            // A periodic snapshot has no requester and is never delivered; only requested outcomes go out
            _snapshotWrittenNotifier.OnSnapshotWritten.Where(w => w.HasRequester).Subscribe(OnSnapshotWritten);

            #region Internal

            void OnSnapshotWritten(SnapshotWritten written)
            {
                // 要求元1人へ返す。全接続へ配ると無関係なクライアントが他人の完了とサーバー側のパスを受け取る
                // Delivered to the single requester; broadcasting would hand unrelated clients someone else's completion and the server-side path
                if (!_requesterRegistry.TryTakeRequester(written.RequestId, out var requesterPlayerId)) return;

                // ファイル一覧の出所はリングの権威リスト。配信層がディスクを舐めると剪定と競合し順序も辞書順になる
                // The file list comes from the ring's authoritative state; scanning disk here would race pruning and order names lexicographically
                var payload = MessagePackSerializer.Serialize(new BugReportCaptureCompletedMessagePack(
                    written.RequestId, written.Tick, written.Success, written.SnapshotDirectory,
                    written.SnapshotFileNames.ToList(), written.PacketLogFileNames.ToList(), _serverDataDirectory.Root));
                _eventProtocolProvider.AddEvent(requesterPlayerId, EventTag, payload);
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

            // 書き出しが成功したか。失敗も配信されるので、要求元はこれを見て待ちを打ち切る
            // Whether the write succeeded; failures are broadcast too, so the requester stops waiting on this flag
            [Key(5)] public bool Success { get; set; }

            // 記録時にサーバーがマスタとmodを読んだ置き場。再現側はここを渡されないと別のマスタで再生する
            // Where the server read masters and mods at record time; without it the reproduction side replays against different masters
            [Key(6)] public string ServerDataDirectory { get; set; }

            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public BugReportCaptureCompletedMessagePack() { }

            public BugReportCaptureCompletedMessagePack(long captureId, ulong tick, bool success, string snapshotDirectory, List<string> snapshotFileNames, List<string> packetLogFileNames, string serverDataDirectory)
            {
                CaptureId = captureId;
                Tick = tick;
                Success = success;
                SnapshotDirectory = snapshotDirectory;
                SnapshotFileNames = snapshotFileNames;
                PacketLogFileNames = packetLogFileNames;
                ServerDataDirectory = serverDataDirectory;
            }
        }
    }
}
