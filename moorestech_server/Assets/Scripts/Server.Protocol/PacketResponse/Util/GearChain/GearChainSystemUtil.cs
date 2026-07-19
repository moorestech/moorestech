using System;
using Core.Master;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.Gear.Common;
using Game.UnlockState;
using Game.World.Interface.DataStore;
using UnityEngine;
using Game.PlayerInventory.Interface;
using Server.Protocol.PacketResponse.Util.ConnectTool;

namespace Server.Protocol.PacketResponse.Util.GearChain
{
    public static class GearChainSystemUtil
    {
        public static bool TryConnect(Vector3Int posA, Vector3Int posB, int playerId, Guid connectToolGuid, out string error)
        {
            // 接続対象を取得する
            // Acquire target chain poles
            error = string.Empty;
            var foundA = TryGetGearChainPole(posA, out var poleA, out var transformerA);
            var foundB = TryGetGearChainPole(posB, out var poleB, out var transformerB);


            if (!foundA || !foundB)
            {
                error = $"{GearChainPlacementEvaluator.InvalidTargetError} (foundA={foundA}, foundB={foundB})";
                return false;
            }

            if (poleA.BlockInstanceId == poleB.BlockInstanceId)
            {
                error = GearChainPlacementEvaluator.InvalidTargetError;
                return false;
            }

            // 未解放のconnectToolによる接続要求は拒否する
            // Reject connection requests using a connectTool that is not unlocked
            if (!IsConnectToolUnlocked(connectToolGuid))
            {
                error = GearChainPlacementEvaluator.NotUnlockedError;
                return false;
            }

            // 距離・既接続・接続数上限・チェーン素材を共有判定で検証する
            // Validate distance, existing connection, connection limit and chain materials via shared judgement
            var connectionDistance = Vector3Int.Distance(posA, posB);
            var alreadyConnected = poleA.ContainsChainConnection(poleB.BlockInstanceId) || poleB.ContainsChainConnection(poleA.BlockInstanceId);
            var inventory = ServerContext.GetService<IPlayerInventoryDataStore>().GetInventoryData(playerId).MainOpenableInventory;
            var judgement = GearChainPlacementEvaluator.EvaluatePlacement(connectionDistance, poleA.MaxConnectionDistance, poleB.MaxConnectionDistance, alreadyConnected, poleA.IsConnectionFull || poleB.IsConnectionFull, connectToolGuid, inventory.InventoryItems, null);
            if (!judgement.IsPlaceable)
            {
                error = judgement.FailureReason;
                return false;
            }
            var cost = judgement.ChainCost;


            // 接続を確定させる
            // Finalize connection
            var addedA = poleA.TryAddChainConnection(poleB.BlockInstanceId, cost);
            var addedB = addedA && poleB.TryAddChainConnection(poleA.BlockInstanceId, cost);
            if (!addedA || !addedB)
            {
                poleA.TryRemoveChainConnection(poleB.BlockInstanceId, out _);
                poleB.TryRemoveChainConnection(poleA.BlockInstanceId, out _);
                error = "ConnectionLimit";
                return false;
            }

            ConnectToolMaterialConsumer.Consume(cost.Materials, inventory);
            RebuildNetworks(transformerA, transformerB);

            return true;
        }

        // connectToolの解放状態を確認する
        // Check whether the connectTool is unlocked
        public static bool IsConnectToolUnlocked(Guid connectToolGuid)
        {
            var infos = ServerContext.GetService<IGameUnlockStateDataController>().ConnectToolUnlockStateInfos;
            return infos.TryGetValue(connectToolGuid, out var info) && info.IsUnlocked;
        }

        public static bool TryGetGearChainPole(Vector3Int position, out IGearChainPole chainPole, out IGearEnergyTransformer transformer)
        {
            // 指定座標からコンポーネントを解決する
            // Resolve component from position
            chainPole = null;
            transformer = null;

            var blockFound = ServerContext.WorldBlockDatastore.TryGetBlock(position, out var block);

            if (!blockFound)
            {
                return false;
            }

            chainPole = block.GetComponent<IGearChainPole>();
            transformer = block.GetComponent<IGearEnergyTransformer>();
            
            return chainPole != null && transformer != null;
        }

        private static void RebuildNetworks(params IGearEnergyTransformer[] transformers)
        {
            // ネットワークを再構築して回転を再計算する
            // Rebuild gear networks to recalc rotation
            
            foreach (var transformer in transformers) GearNetworkDatastore.RemoveGear(transformer);
            foreach (var transformer in transformers) GearNetworkDatastore.AddGear(transformer);
        }
    }
}
