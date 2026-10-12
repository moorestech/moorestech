using System;
using Client.Game.InGame.BlockSystem.PlaceSystem.Undo.Removal;
using Client.Game.InGame.BlockSystem.StateProcessor.ConnectionLine;
using Client.Game.InGame.Context;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.StateProcessor.GearPole
{
    // 歯車チェーンの切断と復元を送る
    // Send gear-chain disconnect and restore requests
    public sealed class GearChainLineCommands : IConnectionLineCommands
    {
        public ConnectionLineKind Kind => ConnectionLineKind.GearChain;

        public void SendDisconnect(Vector3Int posA, Vector3Int posB)
        {
            ClientContext.VanillaApi.SendOnly.ConnectionLine.DisconnectGearChain(posA, posB);
        }

        public void SendRestore(IRemovalRestoreSender sender, Vector3Int posA, Vector3Int posB, Guid connectToolGuid)
        {
            sender.ConnectGearChain(posA, posB, connectToolGuid);
        }
    }
}
