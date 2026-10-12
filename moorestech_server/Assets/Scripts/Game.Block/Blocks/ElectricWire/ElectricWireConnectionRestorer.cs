using System.Collections.Generic;
using Game.Block.Blocks.ConnectionLine;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.EnergySystem;
using UnityEngine;

namespace Game.Block.Blocks.ElectricWire
{
    // セーブ済み電線接続を検証して台帳へ復元する
    // Validate saved wire connections and restore the ledger
    internal static class ElectricWireConnectionRestorer
    {
        internal static bool Restore(ElectricWireSaveDataJsonObject data, BlockInstanceId selfId, int limit,
            Dictionary<BlockInstanceId, (IElectricWireConnector Connector, ConnectionLineRecord Record)> connections)
        {
            if (data.Connections == null)
            {
                Debug.LogWarning($"[ElectricWire] Saved connections missing: {selfId}");
                return false;
            }
            if (data.Connections.Count == 0) return false;

            foreach (var connection in data.Connections)
            {
                // 不正な端点を診断し、正常な接続は続ける
                // Diagnose invalid endpoints and continue restoring valid connections
                if (connection == null)
                {
                    Debug.LogWarning($"[ElectricWire] Null saved connection skipped: {selfId}");
                    continue;
                }
                if (connection.TargetBlockInstanceId == selfId.AsPrimitive())
                {
                    Debug.LogWarning($"[ElectricWire] Saved self connection skipped: {selfId}");
                    continue;
                }
                if (limit <= connections.Count)
                {
                    Debug.LogWarning($"[ElectricWire] Saved connections exceed limit: {selfId}, limit={limit}");
                    break;
                }
                var targetId = new BlockInstanceId(connection.TargetBlockInstanceId);
                if (connections.ContainsKey(targetId))
                {
                    Debug.LogWarning($"[ElectricWire] Duplicate saved connection: {selfId} -> {targetId}");
                    continue;
                }
                if (!connection.TryToConnectionRecord(out var record))
                {
                    Debug.LogWarning($"[ElectricWire] Saved connection without connectToolGuid skipped: {selfId} -> {targetId}");
                    continue;
                }
                var connector = ResolveTarget(selfId, targetId);
                if (connector == null)
                {
                    continue;
                }
                connections.Add(targetId, (connector, record));
            }
            return true;
        }

        internal static IElectricWireConnector ResolveTarget(BlockInstanceId ownerId, BlockInstanceId targetId)
        {
            var block = ServerContext.WorldBlockDatastore.GetBlock(targetId);
            var connector = block?.GetComponent<IElectricWireConnector>();
            if (connector != null && connector.BlockInstanceId != ownerId) return connector;

            // 消えた相手や自己接続を診断する
            // Diagnose missing partners and rejected self-connections
            Debug.LogWarning($"[ElectricWire] Missing or self connection target: {ownerId} -> {targetId}");
            return null;
        }
    }
}
