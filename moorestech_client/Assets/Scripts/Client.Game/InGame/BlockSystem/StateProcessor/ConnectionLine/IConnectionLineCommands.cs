using System;
using Client.Game.InGame.BlockSystem.PlaceSystem.Undo.Removal;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.StateProcessor.ConnectionLine
{
    // 具体的な線種が切断と復元の送信先を決める
    // The concrete line kind selects its disconnect and restore requests
    public interface IConnectionLineCommands
    {
        void SendDisconnect(Vector3Int posA, Vector3Int posB);
        void SendRestore(IRemovalRestoreSender sender, Vector3Int posA, Vector3Int posB, Guid connectToolGuid);
    }
}
