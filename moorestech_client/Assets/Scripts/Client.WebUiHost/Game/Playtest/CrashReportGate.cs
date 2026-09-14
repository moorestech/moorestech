using System;
using Client.Game.InGame.BugReport.LastSession;
using Cysharp.Threading.Tasks;
using UniRx;
using UnityEngine;

namespace Client.WebUiHost.Game.Playtest
{
    /// <summary>
    /// 前回異常終了の送信確認。応答があるまでゲーム開始を止める（ADR 0040 の言語選択ゲートと同型）。
    /// The previous-crash send confirmation that holds the game start until it is answered (same shape as the ADR 0040 language gate).
    /// </summary>
    public class CrashReportGate
    {
        private readonly UniTaskCompletionSource _responseSource = new();
        private readonly Subject<Unit> _onWaitingChanged = new();
        private readonly ICrashBundleWriter _writer;
        private readonly PreviousSessionArtifacts _artifacts;

        // 待機状態と応答の受付はこのアセンブリ内のtopic・actionだけが触る
        // Only this assembly's topic and action touch the waiting state and the answer intake
        internal bool IsWaitingResponse { get; private set; }
        internal IObservable<Unit> OnWaitingChanged => _onWaitingChanged;

        // 登録は常に無条件、待つかどうかは退避結果から導く（未登録によるWeb側購読の固着を避ける）
        // Registration is always unconditional; whether to wait is derived from the salvage result to avoid a stuck web subscription
        // 異常終了なら常に確認する。退避物ゼロでも説明文だけの箱には価値があるので待機条件から外さない
        // Always ask after an unclean exit; a description-only box still has value, so an empty salvage does not skip the wait
        internal CrashReportGate(ICrashBundleWriter writer, PreviousSessionArtifacts artifacts)
        {
            _writer = writer;
            _artifacts = artifacts;
            IsWaitingResponse = !artifacts.PreviousExitWasClean;
            if (!IsWaitingResponse) _responseSource.TrySetResult();
        }

        // 開始側（Client.Starter）が待ち合わせる唯一の窓口なのでここだけpublicに残す
        // The single window the starter assembly awaits on, so this alone stays public
        public UniTask WaitForResponseAsync()
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

            // 「送らない」でも退避物は消さない。last-session は退避のたびに空になるので1世代だけ残る
            // Skipping keeps the salvage: last-session is emptied on every salvage, so exactly one generation survives
            if (!send)
            {
                Debug.Log("前回異常終了の記録は送らないと選ばれました");
                Release();
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
                if (written) Release();
                else ReturnToWaiting();
            }
            return written ? CrashReportResponseResult.Sent : CrashReportResponseResult.WriteFailed;
        }

        private void Release()
        {
            _onWaitingChanged.OnNext(Unit.Default);
            _responseSource.TrySetResult();
        }

        // 「送らない」は常に押せるため、書けないまま待機へ戻しても起動が恒久停止することはない
        // "Do not send" is always available, so returning to waiting after a failed write never halts the boot permanently
        private void ReturnToWaiting()
        {
            Debug.LogError("前回異常終了の箱を書けなかったため確認を閉じません（送り直すか、送らないを選べます）");
            IsWaitingResponse = true;
            _onWaitingChanged.OnNext(Unit.Default);
        }
    }

    public enum CrashReportResponseResult
    {
        Sent,
        Skipped,
        WriteFailed,
        AlreadyResponded
    }
}
