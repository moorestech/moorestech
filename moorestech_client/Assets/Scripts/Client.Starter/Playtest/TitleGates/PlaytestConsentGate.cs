using Client.Game.InGame.BugReport.Playtest;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Client.Starter.Playtest.TitleGates
{
    /// <summary>
    /// 初回だけ「送られる内容」を出して開始を止める（ADR 0061・0065）。表示はタイトルの uGUI が持ち、ここは待機と了解の規則だけを持つ。
    /// Shows what will be sent and holds the start on the first boot only (ADR 0061, 0065); the title's uGUI owns the display and this owns only the wait and acknowledgement rules.
    /// </summary>
    internal sealed class PlaytestConsentGate
    {
        private readonly UniTaskCompletionSource _acknowledgeSource = new();

        internal bool IsWaitingAcknowledgement { get; private set; }

        // 待つかどうかは初期状態で決める。既読フラグの読み取りは組み立て側（PlaytestTitleGates.Compose）が持つ
        // Whether to wait is fixed at construction; reading the read flag belongs to the assembler (PlaytestTitleGates.Compose)
        internal PlaytestConsentGate(bool startsWaiting)
        {
            IsWaitingAcknowledgement = startsWaiting;
            if (!startsWaiting) _acknowledgeSource.TrySetResult();
        }

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
                _acknowledgeSource.TrySetResult();
            }
            return PlaytestConsentResult.Acknowledged;
        }
    }

    public enum PlaytestConsentResult
    {
        Acknowledged,
        AlreadyAcknowledged,
        NotAsked
    }
}
