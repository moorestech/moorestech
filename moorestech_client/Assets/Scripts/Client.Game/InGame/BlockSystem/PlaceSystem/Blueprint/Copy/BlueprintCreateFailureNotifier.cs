using System;
using Client.Game.InGame.UI.Notification;
using Server.Event.Notification;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Blueprint.Copy
{
    /// <summary>
    ///     作成失敗を開発ログへ出し、サーバーが通知しない理由だけ画面通知する
    ///     Logs creation failure and shows a UI notice only for reasons the server does not notify
    /// </summary>
    public static class BlueprintCreateFailureNotifier
    {
        public static void NotifyFailure(BlueprintCreateResult result, ClientLocalNotificationSource source, Vector3Int min, Vector3Int max, string name)
        {
            if (result.Success) throw new InvalidOperationException("Cannot notify a successful blueprint creation as failure");
            Debug.LogError($"[BlueprintCopy] create rejected: {result.Failure} box={min}-{max} name={name}");

            // 未解放の拒否はサーバーが denied.blueprint.NotUnlocked で通知するため、二重に出さずログだけにする
            // The server already notifies a locked rejection as denied.blueprint.NotUnlocked, so log only and never show it twice
            if (result.Failure == BlueprintCreateFailure.NotUnlocked) return;
            source.Notify(NotificationMessagePack.CreateOperationDenied($"denied.blueprintCreate.{result.Failure}", Array.Empty<string>()));
        }
    }
}
