using System.Collections.Generic;
using Client.Game.InGame.BugReport.Recording;
using Client.Game.InGame.UI.UIState;
using Cysharp.Threading.Tasks;

namespace Client.Game.InGame.BugReport.Capture
{
    // 確保セッションの外向きの状態。ポーズメニューはこの3つだけを見る
    // The capture session's outward state; the pause menu looks only at these three
    public sealed class BugReportCaptureStatus
    {
        public bool HasSession { get; }
        public bool CapturePending { get; }
        public IReadOnlyList<string> Missing { get; }

        public BugReportCaptureStatus(bool hasSession, bool capturePending, IReadOnlyList<string> missing)
        {
            HasSession = hasSession;
            CapturePending = capturePending;
            Missing = missing;
        }
    }

    // 確保した記録一式。送信時に BugReportBundleWriter へ渡す
    // Everything captured for one report; handed to BugReportBundleWriter on send
    public sealed class BugReportCapturedData
    {
        public long CaptureId;
        public ulong ReportTick;
        // サーバーが実際にマスタを読んだ置き場。manifest に載せないと再現側が別のマスタで再生する
        // Where the server actually read its masters; without it in the manifest the reproduction replays different masters
        public string ServerDataDirectory;
        // Escape時点でサーバーの置き場から退避した実体の置き場。サーバー側は記入中も剪定を進めるため名前だけでは足りない
        // Where the Escape-moment files were staged; names alone are not enough because the server keeps pruning while the user types
        public string StagedSnapshotDirectory;

        // 記録時のワールド定義（world.json/map.json/terrain）の置き場。剪定対象外なので送信時に読む
        // Root of the recording's world definition (world.json/map.json/terrain); it is never pruned, so it is read at send time
        public string WorldRootDirectory;
        public List<string> SnapshotFileNames = new();
        public List<string> PacketLogFileNames = new();
        public List<string> VideoSegmentFiles = new();
        public IReadOnlyList<(long unixMs, ulong tick)> FrameTicks;
        public IReadOnlyList<UnityLogEntry> Logs;
        public ClientStateSnapshot ClientState;
        public string ScreenshotPath;
        public List<MissingItem> Missing = new();
    }

    // サーバーへの即時スナップショット要求の結果。受理と拒否を型で分ける
    // Outcome of the immediate-snapshot request; acceptance and rejection are distinguished by type
    public readonly struct BugReportServerCaptureRequest
    {
        public readonly bool Accepted;
        public readonly long CaptureId;
        public readonly string RejectedReason;

        public BugReportServerCaptureRequest(bool accepted, long captureId, string rejectedReason)
        {
            Accepted = accepted;
            CaptureId = captureId;
            RejectedReason = rejectedReason;
        }
    }

    // 確保時点で実体を退避した結果。名前ではなく退避先のファイルが Escape の瞬間の記録そのもの
    // Outcome of staging the records at capture time; the staged files, not the names, are the Escape-moment records
    public sealed class StagedServerCapture
    {
        // 退避に丸ごと失敗したときだけ null。個々のファイルの失敗は Missing に載る
        // Null only when staging failed as a whole; per-file failures appear in Missing
        public string StagingDirectory { get; }
        public IReadOnlyList<string> SnapshotFileNames { get; }
        public IReadOnlyList<string> PacketLogFileNames { get; }
        public IReadOnlyList<MissingItem> Missing { get; }

        public StagedServerCapture(string stagingDirectory, IReadOnlyList<string> snapshotFileNames, IReadOnlyList<string> packetLogFileNames, IReadOnlyList<MissingItem> missing)
        {
            StagingDirectory = stagingDirectory;
            SnapshotFileNames = snapshotFileNames;
            PacketLogFileNames = packetLogFileNames;
            Missing = missing;
        }
    }

    // 確保セッションが記録を取る先。実装はゲーム内の取得元とテストのフェイク
    // Where the capture session takes its records from; implemented in-game and by a fake in tests
    public interface IBugReportCaptureSources
    {
        UniTask<BugReportServerCaptureRequest> RequestServerCapture();

        // 現在の画面は外から押し込まれる。確保元がUI状態機械を参照するとDIが循環する
        // The current screen is pushed in from outside; sources referencing the UI state machine makes DI circular
        void SetCurrentUiState(UIStateEnum uiState);

        // 完了イベントを待つ上限。超えたら確保を諦める（テストは即時完了するフェイクへ差し替える）
        // Upper bound on waiting for the completion event; exceeding it abandons the capture
        UniTask WaitServerCaptureTimeout();

        // 完了イベントで受け取った名前のファイルを、サーバーの剪定が届かない場所へ実体ごと退避する
        // Copies the files named by the completion event out to where the server's pruning cannot reach them
        UniTask<StagedServerCapture> StageServerCapture(string snapshotDirectory, IReadOnlyList<string> snapshotFileNames, IReadOnlyList<string> packetLogFileNames);
        void CutRecordingSegment();
        IReadOnlyList<string> CompletedVideoSegments();
        RecordingAvailability GetRecordingAvailability();
        IReadOnlyList<(long unixMs, ulong tick)> FrameTicks();
        IReadOnlyList<UnityLogEntry> Logs();
        ClientStateSnapshot ClientState();
        UniTask<string> CaptureScreenshot();
    }
}
