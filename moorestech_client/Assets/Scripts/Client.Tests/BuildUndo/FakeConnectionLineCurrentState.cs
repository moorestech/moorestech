using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.StateProcessor.ConnectionLine;
using UnityEngine;

namespace Client.Tests.BuildUndo
{
    /// <summary>
    ///     撤去Undoのテストで現在の接続状態を指定する読み取りスタブ
    ///     Read stub supplying current connections to removal-undo tests
    /// </summary>
    public class FakeConnectionLineCurrentState : IConnectionLineCurrentState
    {
        private readonly List<(ConnectionLineKind kind, Vector3Int posA, Vector3Int posB)> _connections = new();

        public void SetConnected(ConnectionLineKind kind, Vector3Int posA, Vector3Int posB)
        {
            _connections.Add((kind, posA, posB));
        }

        public bool HasConnection(ConnectionLineKind kind, Vector3Int posA, Vector3Int posB)
        {
            foreach (var connection in _connections)
            {
                if (connection.kind != kind) continue;
                if (connection.posA == posA && connection.posB == posB ||
                    connection.posA == posB && connection.posB == posA) return true;
            }
            return false;
        }
    }
}
