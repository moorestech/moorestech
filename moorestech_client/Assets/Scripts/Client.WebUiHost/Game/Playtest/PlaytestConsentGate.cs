using System;
using Client.Game.InGame.BugReport.Playtest;
using Client.WebUiHost.Game.StartGates;
using Cysharp.Threading.Tasks;
using UniRx;
using UnityEngine;

namespace Client.WebUiHost.Game.Playtest
{
    /// <summary>
    /// 初回起動だけ「送られる内容」を出して開始を止める（ADR 0040 の言語選択ゲート・CrashReportGateと同型）。
    /// Shows what will be sent and holds the start on the first boot only (same shape as the ADR 0040 language gate and CrashReportGate).
    /// </summary>
    public class PlaytestConsentGate : IStartGateWaitState
    {
        private readonly UniTaskCompletionSource _acknowledgeSource = new();
        private readonly Subject<Unit> _onWaitingChanged = new();

        // 待機状態と了解の受付はこのアセンブリ内のtopic・actionだけが触る
        // Only this assembly's topic and action touch the waiting state and the acknowledgement intake
        internal bool IsWaitingAcknowledgement { get; private set; }
        internal IObservable<Unit> OnWaitingChanged => _onWaitingChanged;
        bool IStartGateWaitState.IsWaiting => IsWaitingAcknowledgement;
        IObservable<Unit> IStartGateWaitState.OnWaitingChanged => _onWaitingChanged;

        // 登録は常に無条件、待つかどうかは初期状態で決める（未登録によるWeb側購読の固着を避ける）
        // Registration is always unconditional; whether to wait is decided by the initial state to avoid a stuck web subscription
        internal PlaytestConsentGate(bool startsWaiting)
        {
            IsWaitingAcknowledgement = startsWaiting;
            if (!startsWaiting) _acknowledgeSource.TrySetResult();
        }

        // 待ち合わせは開始ゲートの持ち手（PlaytestStartGateHandles）が順序どおりに行う
        // The start-gate handles (PlaytestStartGateHandles) await this in the boot order
        internal UniTask WaitForAcknowledgementAsync()
        {
            return _acknowledgeSource.Task;
        }

        // 了解は1回だけ効く。既読フラグはここで書き、次回以降は待機せず素通りする
        // Only the first acknowledgement takes effect; the read flag is written here so later boots pass straight through
        internal PlaytestConsentResult Acknowledge()
        {
            if (!IsWaitingAcknowledgement)
            {
                Debug.LogWarning("PlaytestConsentGate: 了解済みまたは待機していないゲートへ了解が届いたため無視します");
                return PlaytestConsentResult.AlreadyAcknowledged;
            }
            IsWaitingAcknowledgement = false;

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
