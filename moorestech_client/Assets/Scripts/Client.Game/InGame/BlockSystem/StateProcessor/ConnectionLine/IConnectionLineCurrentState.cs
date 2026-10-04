using UnityEngine;

namespace Client.Game.InGame.BlockSystem.StateProcessor.ConnectionLine
{
    /// <summary>
    ///     撤去Undo時に現在の接続線を読み取る口
    ///     Read access to current connection lines during removal undo
    /// </summary>
    public interface IConnectionLineCurrentState
    {
        bool HasConnection(ConnectionLineKind kind, Vector3Int posA, Vector3Int posB);
    }
}
