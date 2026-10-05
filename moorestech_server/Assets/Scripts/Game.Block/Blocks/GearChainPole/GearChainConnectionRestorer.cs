using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.Gear.Common;
using UnityEngine;

namespace Game.Block.Blocks.GearChainPole
{
    // 保存済みの接続相手をワールドから解決して台帳へ戻す
    // Resolve saved partners from the world and restore the connection ledger
    public static class GearChainConnectionRestorer
    {
        public static void Restore(GearChainPoleSaveDataJsonObject data, BlockInstanceId ownerId, int maxConnectionCount,
            IGearChainConnectionLookup lookup, IGearChainConnectionMutation mutation)
        {
            mutation.Clear();
            if (data.Connections == null)
            {
                Debug.LogWarning($"[GearChain] Saved connections are null: {ownerId}");
                return;
            }

            foreach (var connection in data.Connections)
            {
                // 破損した接続は理由を残して復元を見送る
                // Skip malformed connections with a diagnostic reason
                var partnerId = new BlockInstanceId(connection.TargetBlockInstanceId);
                if (lookup.Contains(partnerId))
                {
                    Debug.LogWarning($"[GearChain] Duplicate saved connection: {ownerId} -> {partnerId}");
                    continue;
                }
                if (maxConnectionCount <= lookup.Count)
                {
                    Debug.LogWarning($"[GearChain] Saved connections exceed limit: {ownerId}, limit={maxConnectionCount}");
                    break;
                }

                // 接続相手と保存した種類・素材を同時に復元する
                // Restore each partner together with the saved tool and materials
                var transformer = ResolveTarget(ownerId, partnerId);
                if (transformer == null) continue;
                mutation.Add(partnerId, transformer, connection.ToConnectionRecord());
            }
        }

        public static IGearEnergyTransformer ResolveTarget(BlockInstanceId ownerId, BlockInstanceId partnerId)
        {
            var block = ServerContext.WorldBlockDatastore.GetBlock(partnerId);
            var transformer = block?.GetComponent<IGearEnergyTransformer>();
            if (transformer != null && transformer.BlockInstanceId != ownerId) return transformer;

            // 消えた相手や自己接続を拒否した理由を残す
            // Diagnose missing partners and rejected self-connections
            Debug.LogWarning($"[GearChain] Missing or self connection target: {ownerId} -> {partnerId}");
            return null;
        }
    }
}
