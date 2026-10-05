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
        public static bool TryConnect(Vector3Int posA, Vector3Int posB, int playerId, Guid connectToolGuid, out GearChainPlacementFailureReason failureReason)
        {
            // 接続対象を取得する
            // Acquire target chain poles
            failureReason = GearChainPlacementFailureReason.None;
            var foundA = TryGetGearChainPole(posA, out var poleA, out _);
            var foundB = TryGetGearChainPole(posB, out var poleB, out _);


            if (!foundA || !foundB)
            {
                failureReason = GearChainPlacementFailureReason.InvalidTarget;
                return false;
            }

            if (poleA.BlockInstanceId == poleB.BlockInstanceId)
            {
                failureReason = GearChainPlacementFailureReason.InvalidTarget;
                return false;
            }

            // 未解放のconnectToolによる接続要求は拒否する
            // Reject connection requests using a connectTool that is not unlocked
            if (!IsConnectToolUnlocked(connectToolGuid))
            {
                failureReason = GearChainPlacementFailureReason.NotUnlocked;
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
                failureReason = judgement.FailureReason;
                return false;
            }
            var record = judgement.ChainRecord;


            // 接続を確定させる
            // Finalize connection
            var addedA = poleA.TryAddChainConnection(poleB.BlockInstanceId, record);
            var addedB = addedA && poleB.TryAddChainConnection(poleA.BlockInstanceId, record);
            if (!addedA || !addedB)
            {
                // 追加できた側だけ戻す
                // Roll back only the side that was actually added
                if (addedA) poleA.TryRemoveChainConnection(poleB.BlockInstanceId, out _);
                if (addedB) poleB.TryRemoveChainConnection(poleA.BlockInstanceId, out _);
                failureReason = GearChainPlacementFailureReason.ConnectionLimit;
                return false;
            }

            ConnectToolMaterialConsumer.Consume(record.Materials, inventory);

            return true;
        }

        public static bool TryDisconnect(Vector3Int posA, Vector3Int posB, int playerId, out GearChainDisconnectFailureReason failureReason)
        {
            // 両端のポールを解決する
            // Resolve both poles
            failureReason = GearChainDisconnectFailureReason.None;
            if (!TryGetGearChainPole(posA, out var poleA, out _) || !TryGetGearChainPole(posB, out var poleB, out _))
            {
                failureReason = GearChainDisconnectFailureReason.InvalidTarget;
                return false;
            }

            // 相互接続でなければ切断できない
            // A pair that is not connected both ways cannot be disconnected
            if (!poleA.TryGetChainConnectionRecord(poleB.BlockInstanceId, out var record) || !poleB.ContainsChainConnection(poleA.BlockInstanceId))
            {
                failureReason = GearChainDisconnectFailureReason.NotConnected;
                return false;
            }

            // 返却が入らないなら切断させない（返却消滅の防止。電線の切断と同じ順序）
            // Refuse when the refund cannot fit, preventing item loss (same order as the wire disconnect)
            var inventory = ServerContext.GetService<IPlayerInventoryDataStore>().GetInventoryData(playerId).MainOpenableInventory;
            if (!ConnectToolMaterialConsumer.TryCreateFittingRefund(record.Materials, inventory, out var refundStacks))
            {
                failureReason = GearChainDisconnectFailureReason.InventoryFull;
                return false;
            }

            // 双方から外して返却する。歯車網のdirty化は除去メソッド自身が行う
            // Remove from both sides and refund; the removal itself marks the gear topology dirty
            poleA.TryRemoveChainConnection(poleB.BlockInstanceId, out _);
            poleB.TryRemoveChainConnection(poleA.BlockInstanceId, out _);
            foreach (var refundStack in refundStacks) inventory.InsertItem(refundStack);
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

    }
}
