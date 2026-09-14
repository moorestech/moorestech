using System;
using Client.Game.InGame.BugReport.Playtest;
using Cysharp.Threading.Tasks;
using UniRx;
using UnityEngine;

namespace Client.WebUiHost.Game.Playtest
{
    /// <summary>
    /// 初回起動だけ「送られる内容」を出して開始を止める（ADR 0040 の言語選択ゲート・CrashReportGateと同型）。
    /// Shows what will be sent and holds the start on the first boot only (same shape as the ADR 0040 language gate and CrashReportGate).
    /// 生成は PlaytestGateBinder 経由に限る。Client.Tests から internal が見えないため公開面は public に留める。
    /// Only PlaytestGateBinder constructs this; the surface stays public because Client.Tests cannot see internals.
    /// </summary>
    public class PlaytestConsentGate
    {
        private readonly UniTaskCompletionSource _acknowledgeSource = new();
        private readonly Subject<Unit> _onWaitingChanged = new();
        private bool _isWaiting;

        public IObservable<Unit> OnWaitingChanged => _onWaitingChanged;

        // 登録は常に無条件、待つかどうかは初期状態で決める（未登録によるWeb側購読の固着を避ける）
        // Registration is always unconditional; whether to wait is decided by the initial state to avoid a stuck web subscription
        public PlaytestConsentGate(bool startsWaiting)
        {
            _isWaiting = startsWaiting;
            if (!startsWaiting) _acknowledgeSource.TrySetResult();
        }

        public bool IsWaitingAcknowledgement()
        {
            return _isWaiting;
        }

        // 開始側（Client.Starter）が待ち合わせる唯一の窓口
        // The single window the starter assembly awaits on
        public UniTask WaitForAcknowledgementAsync()
        {
            return _acknowledgeSource.Task;
        }

        // 了解は1回だけ効く。既読フラグはここで書き、次回以降は待機せず素通りする
        // Only the first acknowledgement takes effect; the read flag is written here so later boots pass straight through
        public PlaytestConsentResult Acknowledge()
        {
            if (!_isWaiting)
            {
                Debug.LogWarning("PlaytestConsentGate: 了解済みまたは待機していないゲートへ了解が届いたため無視します");
                return PlaytestConsentResult.AlreadyAcknowledged;
            }
            _isWaiting = false;

            // フラグ書き込みに関わらずゲートは必ず閉じる。例外で抜けても起動が永久に止まらないよう解除はfinallyに置く
            // The gate always closes regardless of the flag write; releasing in finally keeps an escaping exception from halting the startup forever
            try
            {
                PlaytestConsentFlag.Acknowledge();
            }
            finally
            {
                _onWaitingChanged.OnNext(Unit.Default);
                _acknowledgeSource.TrySetResult();
            }
            return PlaytestConsentResult.Acknowledged;
        }
    }

    public enum PlaytestConsentResult
    {
        Acknowledged,
        AlreadyAcknowledged
    }
}
