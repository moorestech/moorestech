using Client.Game.InGame.BugReport.Capture;
using Client.Game.InGame.BugReport.Playtest;
using Client.Game.InGame.Playtest.Progress;
using Client.PlaytestReceiver;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Client.Game.InGame.BugReport.Submit
{
    // 確保記録を箱へ書き送信要求する（ポーズ/配布共有）
    // Writes captured records into a box and requests upload (shared by pause menu / distribution smoke)
    public sealed class BugReportSubmitter
    {
        private readonly BugReportBundleWriter _writer;
        private readonly BugReportCaptureSession _session;
        private readonly IPlaytestProgressSink _progressSink;
        private readonly IPlaytestUploadRequester _uploadRequester;

        public BugReportSubmitter(BugReportBundleWriter writer, BugReportCaptureSession session, IPlaytestProgressSink progressSink, IPlaytestUploadRequester uploadRequester)
        {
            _writer = writer;
            _session = session;
            _progressSink = progressSink;
            _uploadRequester = uploadRequester;
        }

        public async UniTask<BugReportSubmitResult> SubmitAsync(string description, PlaytestReportKind kind)
        {
            // 確保中・確保なし・二重送信の判定は確保セッションが持つ。ここで再実装すると判定の権威が2つになる
            // The capture session owns the pending / no-session / double-send decision; re-implementing it here would create a second authority
            var ticket = _session.TryBeginSubmit();
            if (!ticket.Allowed)
            {
                Debug.LogWarning($"プレイ報告の送信が確保セッションに拒否されました code:{ticket.RefusedCode}");
                return BugReportSubmitResult.Fail(ticket.RefusedCode, "");
            }

            var result = await _writer.WriteAsync(ticket.Data, description, kind);

            // 書き出しで判明した欠損は確保状態へ戻す。戻さないと報告者は欠けたまま送ったことを知る機会が無い
            // Missing items found while writing go back into the capture state; otherwise the reporter never learns what was dropped
            _session.CompleteSubmit(ticket.Data, result.Ready, result.Missing);

            // READYの無い箱は運搬されない。成功として返すと送ったつもりのまま何も届かない
            // A box without READY is never shipped; returning success would leave a lost report believed sent
            if (!result.Ready)
            {
                Debug.LogError($"バグ報告を書き出せませんでした（運搬されません） {result.BundleDirectory}");
                return BugReportSubmitResult.Fail(BugReportSubmitResult.BundleWriteFailed, result.BundleDirectory);
            }

            Debug.Log($"バグ報告を書き出しました {result.BundleDirectory} missing:{result.Missing.Count}");

            // 送信は購読で観測できないので、成功した操作の直後にプッシュする
            // A send is not observable through any subscription, so it is pushed right after the successful operation
            _progressSink.RecordReportSent(kind);

            // 書き出した箱の送信を要求する。実際に送るかは走行役がゲート判定から決める
            // Ask for the freshly written box to ship; the runner decides from the gate verdict whether it actually ships
            _uploadRequester.RequestUpload();
            return BugReportSubmitResult.Succeed(result.BundleDirectory);
        }
    }
}
