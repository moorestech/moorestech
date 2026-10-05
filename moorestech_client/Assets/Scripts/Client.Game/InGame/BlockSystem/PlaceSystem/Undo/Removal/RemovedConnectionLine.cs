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
        private readonly IConnectionLineCommands _commands;

        public RemovedConnectionLine(ConnectionLineKind kind, Vector3Int posA, Vector3Int posB, Guid connectToolGuid, IConnectionLineCommands commands)
        {
            // 端点順に依らず同じキーになるよう正規化する
            // Normalize so the key does not depend on endpoint order
            var aFirst = IsOrdered(posA, posB);
            _kind = kind;
            _posA = aFirst ? posA : posB;
            _posB = aFirst ? posB : posA;
            _connectToolGuid = connectToolGuid;
            _commands = commands;

            #region Internal

            bool IsOrdered(Vector3Int a, Vector3Int b)
            {
                if (a.x != b.x) return a.x < b.x;
                if (a.y != b.y) return a.y < b.y;
                return a.z <= b.z;
            }

            #endregion
        }

        public static void Capture(ConnectionLineDeleteTarget line, RemovedObjectCollector collector)
        {
            // 端点未解決なら復元先が無いため件数に含める
            // Count a line with unresolved endpoints because it has no restore target
            if (!line.TryGetRestoreData(out var posA, out var posB))
            {
                collector.AddUnrecordable($"line endpoint block not found from={line.FromId} to={line.ToId}");
                return;
            }
            collector.Add(new RemovedConnectionLine(line.Kind, posA, posB, line.ConnectToolGuid, line.GetLineCommands()));
        }

        public object RestoreKey => (_kind, _posA, _posB);

        public BlockRestoreOutcome AppendBlockRestore(List<PlaceInfo> placeInfos, IBlockOccupancyQuery occupancy)
        {
            return BlockRestoreOutcome.NotABlock;
        }

        public void SendConnectionRestore(IRemovalRestoreSender sender)
        {
            _commands.SendRestore(sender, _posA, _posB, _connectToolGuid);
        }
    }
}
