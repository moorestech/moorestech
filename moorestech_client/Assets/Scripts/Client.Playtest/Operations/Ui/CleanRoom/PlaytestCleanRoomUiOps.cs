using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Client.Playtest.Operations.Ui
{
    /// <summary>
    /// 開いている室内機械の画面で「失敗率の改善方法」のモーダルを開き、中身を記録して撮り、閉じる
    /// On an open interior-machine screen, open the improvement modal, record and shoot its contents, then close it
    /// </summary>
    public static class PlaytestCleanRoomUiOps
    {
        private const float WaitSeconds = 10f;

        public static async UniTask CheckCleanRoomYieldImprovementModal(this PlaytestDriver p, string screenshotName)
        {
            // ボタンを押し、表の1行目が出るまで待つ
            // Press the button and wait for the first table row
            await p.UntilUiElement("clean-room-yield-improvement-open", WaitSeconds);
            await p.ClickUi("clean-room-yield-improvement-open");
            await PlaytestUiElementOps.WaitUiTextContains("modal-table-row-0", "%", WaitSeconds);

            // 本文（今の値）と表を記録し、割合が出ていることを見る
            // Record the message (current values) and the table, and check shares are shown
            var message = (await PlaytestUiElementOps.FindUi("modal-message")).Text;
            var table = (await PlaytestUiElementOps.FindUi("modal-table")).Text;
            p.Note($"失敗率の改善方法: {message} || {table.Replace("\n", " | ")}");
            p.Assert(message.Contains("%") && table.Contains("%"), $"改善方法のモーダルに今の値と差分の割合が出る(本文={message})");
            await p.Screenshot(screenshotName);

            // 閉じるボタンで閉じ、モーダルが消えるまで待つ
            // Close with the close button and wait until the modal is gone
            await p.ClickUi("modal-button");
            var deadline = Time.realtimeSinceStartup + WaitSeconds;
            while ((await PlaytestUiElementOps.FindUi("modal")).IsFound)
            {
                if (deadline < Time.realtimeSinceStartup)
                {
                    p.Assert(false, "改善方法のモーダルが閉じるボタンで閉じる");
                    return;
                }
                await UniTask.DelayFrame(2, cancellationToken: p.CancellationToken);
            }
            p.Assert(true, "改善方法のモーダルが閉じるボタンで閉じる");
        }
    }
}
