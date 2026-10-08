using System;
using Client.Game.InGame.UI.Notification;
using Server.Event.Notification;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Blueprint.Copy
{
    /// <summary>
    ///     作成失敗を開発ログと理由別の画面通知へ同時に出す
    ///     Reports creation failure to both developer log and reason-specific UI notice
    /// </summary>
    public static class BlueprintCreateFailureNotifier
    {
        public static void NotifyFailure(BlueprintCreateResult result, ClientLocalNotificationSource source, Vector3Int min, Vector3Int max, string name)
        {
            if (result.Success) throw new InvalidOperationException("Cannot notify a successful blueprint creation as failure");
            Debug.LogError($"[BlueprintCopy] create rejected: {result.Failure} box={min}-{max} name={name}");
            source.Notify(NotificationMessagePack.CreateOperationDenied($"denied.blueprintCreate.{result.Failure}", Array.Empty<string>()));
        }
    }
}
