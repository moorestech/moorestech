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
    /// 生成は PlaytestGateBinder 経由に限る。Client.Tests から internal が見えないため公開面は public に留める。
    /// Only PlaytestGateBinder constructs this; the surface stays public because Client.Tests cannot see internals.
    /// </summary>
    public class CrashReportGate
    {
        private readonly UniTaskCompletionSource _responseSource = new();
        private readonly Subject<Unit> _onWaitingChanged = new();
        private readonly ICrashBundleWriter _writer;
        private readonly PreviousSessionArtifacts _artifacts;
        private bool _isWaitingResponse;

        public string LastWrittenBundleDirectory { get; private set; }
        public IObservable<Unit> OnWaitingChanged => _onWaitingChanged;

        // 登録は常に無条件、待つかどうかは初期状態で決める（未登録によるWeb側購読の固着を避ける）
        // Registration is always unconditional; whether to wait is decided by the initial state to avoid a stuck web subscription
        public CrashReportGate(bool startsWaiting, ICrashBundleWriter writer, PreviousSessionArtifacts artifacts)
        {
            _writer = writer;
            _artifacts = artifacts;
            _isWaitingResponse = startsWaiting;
            if (!startsWaiting) _responseSource.TrySetResult();
        }

        public bool IsWaitingSelection()
        {
            return _isWaitingResponse;
        }

        // 開始側（Client.Starter）が待ち合わせる唯一の窓口
        // The single window the starter assembly awaits on
        public UniTask WaitForResponseAsync()
        {
            return _responseSource.Task;
        }

        // 応答は1回だけ効く。二重クリックと再送は「応答済み」として区別し、成功と一律に丸めない
        // Only the first answer takes effect; double clicks and resends are distinguished instead of folded into success
        public CrashReportResponseResult Respond(bool send, string description)
        {
            if (!_isWaitingResponse)
            {
                Debug.LogWarning("CrashReportGate: 応答済みまたは待機していないゲートへ応答が届いたため無視します");
                return CrashReportResponseResult.AlreadyResponded;
            }
            _isWaitingResponse = false;

            // 箱を書けたかに関わらずゲートは必ず閉じる。書き出しが例外で抜けても起動が永久に止まらないよう解除はfinallyに置く
            // The gate always closes regardless of the write; releasing in finally keeps an escaping exception from halting the startup forever
            try
            {
                // 「送らない」でも退避物は消さない。last-session は退避のたびに空になるので1世代だけ残る
                // Skipping keeps the salvage: last-session is emptied on every salvage, so exactly one generation survives
                if (send) LastWrittenBundleDirectory = _writer.Write(_artifacts, description ?? "");
                else Debug.Log("前回異常終了の記録は送らないと選ばれました");
            }
            finally
            {
                _onWaitingChanged.OnNext(Unit.Default);
                _responseSource.TrySetResult();
            }
            return send ? CrashReportResponseResult.Sent : CrashReportResponseResult.Skipped;
        }
    }

    public enum CrashReportResponseResult
    {
        Sent,
        Skipped,
        AlreadyResponded
    }
}
