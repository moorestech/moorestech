using System;
using Client.Game.InGame.BlockSystem.PlaceSystem.Undo.Removal;
using Client.Game.InGame.BlockSystem.StateProcessor.ConnectionLine;
using UnityEngine;

namespace Client.Tests.BuildUndo
{
    public sealed class FakeConnectionLineCommands : IConnectionLineCommands
    {
        private readonly ConnectionLineKind _kind;
        public ConnectionLineKind Kind => _kind;

        public FakeConnectionLineCommands(ConnectionLineKind kind)
        {
            _kind = kind;
        }

        public void SendDisconnect(Vector3Int posA, Vector3Int posB)
        {
        }

        public void SendRestore(IRemovalRestoreSender sender, Vector3Int posA, Vector3Int posB, Guid connectToolGuid)
        {
            if (_kind == ConnectionLineKind.ElectricWire) sender.ConnectElectricWire(posA, posB, connectToolGuid);
            else sender.ConnectGearChain(posA, posB, connectToolGuid);
        }
    }
}
