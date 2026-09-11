using System.Collections.Generic;
using Cysharp.Threading.Tasks;

namespace Client.Game.InGame.BugReport
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
        public string SnapshotDirectory;
        public List<string> SnapshotFileNames = new();
        public List<string> PacketLogFileNames = new();
        public List<string> VideoSegmentFiles = new();
        public IReadOnlyList<(long unixMs, ulong tick)> FrameTicks;
        public IReadOnlyList<UnityLogEntry> Logs;
        public ClientStateSnapshot ClientState;
        public string ScreenshotPath;
        public List<MissingItem> Missing = new();
    }

    // 確保セッションが記録を取る先。実装はゲーム内の取得元とテストのフェイク
    // Where the capture session takes its records from; implemented in-game and by a fake in tests
    public interface IBugReportCaptureSources
    {
        UniTask<long> RequestServerCapture();
        void CutRecordingSegment();
        IReadOnlyList<string> CompletedVideoSegments();
        string RecordingUnavailableReason();
        IReadOnlyList<(long unixMs, ulong tick)> FrameTicks();
        IReadOnlyList<UnityLogEntry> Logs();
        ClientStateSnapshot ClientState();
        UniTask<string> CaptureScreenshot();
    }
}
