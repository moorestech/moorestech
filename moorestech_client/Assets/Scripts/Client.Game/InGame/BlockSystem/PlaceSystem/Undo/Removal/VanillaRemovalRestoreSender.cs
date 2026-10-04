using System;
using System.Collections.Generic;
using Client.Game.InGame.Context;
using Core.Master;
using Game.Train.SaveLoad;
using Server.Event.Notification;
using Server.Protocol.PacketResponse;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Undo.Removal
{
    /// <summary>
    ///     撤去Undoの復元要求をVanillaApiのSendOnlyで送る本番実装。拒否はサーバーが通知で返す
    ///     Production sender using VanillaApi SendOnly; refusals come back as server notifications
    /// </summary>
    public class VanillaRemovalRestoreSender : IRemovalRestoreSender
    {
        // Undoの再設置は自動接続を止め、記録した線だけを後続で引き直す
        // Undo re-placement suppresses auto-connect; only recorded lines are re-drawn afterwards
        public void PlaceBlocks(List<PlaceInfo> placeInfos)
        {
            ClientContext.VanillaApi.SendOnly.PlaceBlock(placeInfos, BlockPlacementWiring.NoAutoConnect);
        }

        public void ConnectElectricWire(Vector3Int posA, Vector3Int posB, Guid connectToolGuid)
        {
            ClientContext.VanillaApi.SendOnly.ConnectElectricWire(posA, posB, connectToolGuid);
        }

        public void ConnectGearChain(Vector3Int posA, Vector3Int posB, Guid connectToolGuid)
        {
            ClientContext.VanillaApi.SendOnly.ConnectGearChain(posA, posB, connectToolGuid);
        }

        public void ConnectRail(ConnectionDestination from, ConnectionDestination to, Guid connectToolGuid)
        {
            ClientContext.VanillaApi.SendOnly.ConnectRailByDestination(from, to, connectToolGuid);
        }

        // サーバー通知と同じ表示面へクライアント側の取りこぼし件数を流す
        // Push the client-side skipped count onto the same display surface as server notifications
        public void NotifyRestoreSkipped(int skippedCount)
        {
            ClientDIContext.ClientLocalNotificationSource.Notify(NotificationMessagePack.CreateOperationDenied("denied.undoRestoreSkipped", new[] { skippedCount.ToString() }));
        }
    }
}
