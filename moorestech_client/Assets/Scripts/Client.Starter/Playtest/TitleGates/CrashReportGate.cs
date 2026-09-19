using Client.Game.InGame.BugReport.LastSession;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Client.Starter.Playtest.TitleGates
{
    /// <summary>
    /// 前回異常終了の送信確認。応答があるまで開始を止める（ADR 0061・0065）。表示はタイトルの uGUI が持つ。
    /// The previous-crash send confirmation that holds the start until it is answered (ADR 0061, 0065); the title's uGUI owns the display.
    /// </summary>
    public sealed class CrashReportGate
    {
        private readonly UniTaskCompletionSource _responseSource = new();
        private readonly ICrashBundleWriter _writer;
        private readonly PreviousSessionArtifacts _artifacts;

        internal bool IsWaitingResponse { get; private set; }

        // 異常終了なら常に確認する。退避物ゼロでも説明文だけの箱には価値があるので待機条件から外さない
        // Always ask after an unclean exit; a description-only box still has value, so an empty salvage does not skip the wait
        internal CrashReportGate(ICrashBundleWriter writer, PreviousSessionArtifacts artifacts) : this(writer, artifacts, !artifacts.PreviousExitWasClean)
        {
        }

        private CrashReportGate(ICrashBundleWriter writer, PreviousSessionArtifacts artifacts, bool isWaitingResponse)
        {
            _writer = writer;
            _artifacts = artifacts;
            IsWaitingResponse = isWaitingResponse;
            if (!IsWaitingResponse) _responseSource.TrySetResult();
        }

        // 無人起動向けの閉じたゲート。退避結果を「正常終了」に偽装して借りず、閉じた状態そのものとして作る（F13）
        // A closed gate for unattended boots, built as closed in its own right instead of borrowing a salvage result disguised as a clean exit (F13)
        internal static CrashReportGate Closed()
        {
            return new CrashReportGate(null, null, false);
        }

        internal UniTask WaitForResponseAsync()
        {
            return _responseSource.Task;
        }

        // 応答は1回だけ効く。二重クリックと再送は「応答済み」として区別し、成功と一律に丸めない
        // Only the first answer takes effect; double clicks and resends are distinguished instead of folded into success
        internal async UniTask<CrashReportResponseResult> RespondAsync(bool send, string description)
        {
            if (!IsWaitingResponse)
            {
                Debug.LogWarning("CrashReportGate: 応答済みまたは待機していないゲートへ応答が届いたため無視します");
                return CrashReportResponseResult.AlreadyResponded;
            }
            IsWaitingResponse = false;

            // 「送らない」でも退避物は消さない。答えた時点で未応答の印を消す（F04）
            // Skipping keeps the salvage; answering clears the pending mark (F04)
            if (!send)
            {
                Debug.Log("前回異常終了の記録は送らないと選ばれました");
                PendingCrashReportMark.Clear(_artifacts.LastSessionDirectory);
                _responseSource.TrySetResult();
                return CrashReportResponseResult.Skipped;
            }

            // 箱を書けなかったら待機へ戻す。唯一の証跡なので、閉じてしまうと二度と送り直せないまま無音で消える
            // A failed write returns the gate to waiting: this is the only evidence, and closing would drop it silently with no way to resend
            // 書き出しが例外で抜けても起動が永久に止まらないよう、解除はfinallyに置く
            // Releasing sits in finally so an escaping exception never halts the startup forever
            var written = false;
            try
            {
                written = await _writer.WriteAsync(_artifacts, description ?? "") != null;
            }
            finally
            {
                if (written)
                {
                    PendingCrashReportMark.Clear(_artifacts.LastSessionDirectory);
                    _responseSource.TrySetResult();
                }
                else ReturnToWaiting();
            }
            return written ? CrashReportResponseResult.Sent : CrashReportResponseResult.WriteFailed;
        }

        // 「送らない」は常に押せるため、書けないまま待機へ戻しても起動が恒久停止することはない
        // "Do not send" is always available, so returning to waiting after a failed write never halts the boot permanently
        private void ReturnToWaiting()
        {
            Debug.LogError("前回異常終了の箱を書けなかったため確認を閉じません（送り直すか、送らないを選べます）");
            IsWaitingResponse = true;
        }
    }

    public enum CrashReportResponseResult
    {
        Sent,
        Skipped,
        WriteFailed,
        AlreadyResponded,
        NotAsked
    }
}
