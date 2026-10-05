using System;
using Client.Game.InGame.BlockSystem.PlaceSystem.Undo.Removal;
using Client.Game.InGame.BlockSystem.StateProcessor.ConnectionLine;
using Client.Game.InGame.Context;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.StateProcessor.ElectricWire
{
    // 電線の切断と復元を送る
    // Send wire disconnect and restore requests
    public sealed class ElectricWireLineCommands : IConnectionLineCommands
    {
        public ConnectionLineKind Kind => ConnectionLineKind.ElectricWire;

        public void SendDisconnect(Vector3Int posA, Vector3Int posB)
        {
            ClientContext.VanillaApi.SendOnly.ConnectionLine.DisconnectElectricWire(posA, posB);
        }

        public void SendRestore(IRemovalRestoreSender sender, Vector3Int posA, Vector3Int posB, Guid connectToolGuid)
        {
            sender.ConnectElectricWire(posA, posB, connectToolGuid);
        }
    }
}
