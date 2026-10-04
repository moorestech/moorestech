using Server.Protocol.PacketResponse;
using System;
using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.StateProcessor.ConnectionLine;
using Core.Master;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Undo.Removal
{
    /// <summary>
    ///     撤去した電線・歯車チェーン1本。端点は座標で持つ（再設置でBlockInstanceIdが変わるため）
    ///     One removed wire or gear chain; endpoints are kept as positions because re-placement changes BlockInstanceIds
    /// </summary>
    public class RemovedConnectionLine : IRemovedObject
    {
        private readonly ConnectionLineKind _kind;
        private readonly Vector3Int _posA;
        private readonly Vector3Int _posB;
        private readonly Guid _connectToolGuid;
        private readonly IConnectionLineCurrentState _currentState;

        public RemovedConnectionLine(ConnectionLineKind kind, Vector3Int posA, Vector3Int posB, Guid connectToolGuid, IConnectionLineCurrentState currentState)
        {
            // 端点順に依らず同じキーになるよう正規化する
            // Normalize so the key does not depend on endpoint order
            var aFirst = IsOrdered(posA, posB);
            _kind = kind;
            _posA = aFirst ? posA : posB;
            _posB = aFirst ? posB : posA;
            _connectToolGuid = connectToolGuid;
            _currentState = currentState;
        }

        public object RestoreKey => (_kind, _posA, _posB);

        public BlockRestoreOutcome AppendBlockRestore(List<PlaceInfo> placeInfos, IBlockOccupancyQuery occupancy)
        {
            return BlockRestoreOutcome.NotABlock;
        }

        public void SendConnectionRestore(IRemovalRestoreSender sender)
        {
            // 既に同じ端点・線種が接続済みならサーバーへ重複要求を送らない
            // Skip the duplicate server request when the same endpoint pair and line kind is already connected
            if (_currentState.HasConnection(_kind, _posA, _posB)) return;

            switch (_kind)
            {
                case ConnectionLineKind.ElectricWire:
                    sender.ConnectElectricWire(_posA, _posB, _connectToolGuid);
                    break;
                case ConnectionLineKind.GearChain:
                    sender.ConnectGearChain(_posA, _posB, _connectToolGuid);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(_kind), _kind, null);
            }
        }

        private static bool IsOrdered(Vector3Int a, Vector3Int b)
        {
            if (a.x != b.x) return a.x < b.x;
            if (a.y != b.y) return a.y < b.y;
            return a.z <= b.z;
        }
    }
}
