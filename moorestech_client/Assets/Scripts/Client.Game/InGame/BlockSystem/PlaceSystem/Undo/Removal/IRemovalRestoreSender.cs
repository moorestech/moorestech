using Server.Protocol.PacketResponse;
using System;
using System.Collections.Generic;
using Core.Master;
using Game.Train.SaveLoad;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Undo.Removal
{
    /// <summary>
    ///     撤去Undoの復元要求をサーバーへ送る口。サーバーは同一クライアントのパケットをFIFO単一スレッドで処理するため送信順＝適用順
    ///     Outlet sending removal-undo restore requests; the server processes a client's packets FIFO on one thread, so send order equals apply order
    /// </summary>
    public interface IRemovalRestoreSender
    {
        void PlaceBlocks(List<PlaceInfo> placeInfos);
        void ConnectElectricWire(Vector3Int posA, Vector3Int posB, Guid connectToolGuid);
        void ConnectGearChain(Vector3Int posA, Vector3Int posB, Guid connectToolGuid);
        void ConnectRail(ConnectionDestination from, ConnectionDestination to, Guid connectToolGuid);

        // クライアント側で戻せなかった件数（占有済み・記録不能）をプレイヤーへ知らせる。サーバー側の拒否はサーバーが通知する
        // Tell the player how many things the client could not restore (occupied / unrecordable); server-side refusals are notified by the server
        void NotifyRestoreSkipped(int skippedCount);
    }
}
