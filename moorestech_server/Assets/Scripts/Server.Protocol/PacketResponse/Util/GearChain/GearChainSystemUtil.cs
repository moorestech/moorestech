using System;
using Core.Master;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.Gear.Common;
using Game.World.Interface.DataStore;
using UnityEngine;
using Game.PlayerInventory.Interface;
using Mooresmaster.Model.BlocksModule;

namespace Server.Protocol.PacketResponse.Util.GearChain
{
    public static class GearChainSystemUtil
    {
        public static bool TryConnect(Vector3Int posA, Vector3Int posB, int playerId, ItemId itemId, out string error)
        {
            // 接続対象を取得する
            // Acquire target chain poles
            error = string.Empty;
            var foundA = TryGetGearChainPole(posA, out var poleA, out var transformerA);
            var foundB = TryGetGearChainPole(posB, out var poleB, out var transformerB);


            if (!foundA || !foundB)
            {
                error = $"InvalidTarget (foundA={foundA}, foundB={foundB})";
                return false;
            }
            
            if (poleA.BlockInstanceId == poleB.BlockInstanceId)
            {
                error = "InvalidTarget";
                return false;
            }
            
            // 接続距離を算出する
            // Calculate connection distance
            var connectionDistance = Vector3Int.Distance(posA, posB);
            var maxDistance = Math.Min(poleA.MaxConnectionDistance, poleB.MaxConnectionDistance);
            if (connectionDistance > maxDistance)
            {
                error = "TooFar";
                return false;
            }

            // 既存接続がある場合は失敗させる
            // Fail when already connected
            if (poleA.ContainsChainConnection(poleB.BlockInstanceId) || poleB.ContainsChainConnection(poleA.BlockInstanceId))
            {
                error = "AlreadyConnected";
                return false;
            }

            // 接続数の上限を確認する
            // Ensure neither pole is at capacity
            if (poleA.IsConnectionFull || poleB.IsConnectionFull)
            {
                error = "ConnectionLimit";
                return false;
            }

            
            // チェーンアイテムを消費する
            // Consume chain item
            if (!CheckEnoughItemInInventory(connectionDistance,itemId, out var cost))
            {
                error = "NoItem";
                return false;
            }


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
            
            Consume(cost);
            RebuildNetworks(transformerA, transformerB);
            
            return true;
            
            #region Internal
            
            bool CheckEnoughItemInInventory(float distance ,ItemId specifiedItemId, out GearChainConnectionCost consumedCost)
            {
                consumedCost = default;
                
                // 設定が無ければ接続できない
                // Cannot connect when no configuration
                var gearChainItems = MasterHolder.BlockMaster.Blocks.GearChainItems;
                if (gearChainItems.Length == 0) return false;
                
                // 指定されたアイテムが設定に含まれているか確認する
                // Check if specified item is in the configuration
                GearChainItemsElement currentGearChainItem = null;
                foreach (var gearChainItem in gearChainItems)
                {
                    var configItemId = MasterHolder.ItemMaster.GetItemId(gearChainItem.ItemGuid);
                    
                    if (configItemId == specifiedItemId)
                    {
                        currentGearChainItem = gearChainItem;
                        break;
                    }
                }
                
                if (currentGearChainItem == null) return false;
                
                var required = Mathf.CeilToInt(distance / currentGearChainItem.ConsumptionPerLength);
                consumedCost = new GearChainConnectionCost(specifiedItemId, required);
                
                // インベントリ内に十分なアイテムがあるか確認する
                // Check if there are enough items in inventory
                var inventory = ServerContext.GetService<IPlayerInventoryDataStore>().GetInventoryData(playerId).MainOpenableInventory;
                var totalCount = 0;
                foreach (var itemStack in inventory.InventoryItems)
                {
                    if (itemStack.Id != itemId) continue;
                    totalCount += itemStack.Count;
                    if (totalCount >= required) return true;
                }

                return false;
            }
            
            void Consume(GearChainConnectionCost consumedCost)
            {
                var inventory = ServerContext.GetService<IPlayerInventoryDataStore>().GetInventoryData(playerId).MainOpenableInventory;
                var remaining = consumedCost.Count;
                
                // スロットを順に減算する
                // Decrease stacks across slots
                for (var i = 0; i < inventory.InventoryItems.Count && 0 < remaining; i++)
                {
                    var itemStack = inventory.InventoryItems[i];
                    if (itemStack.Id != itemId) continue;
                    
                    var consumeAmount = Math.Min(itemStack.Count, remaining);
                    var updated = itemStack.SubItem(consumeAmount);
                    inventory.SetItem(i, updated);
                    
                    remaining -= consumeAmount;
                }
            }
            
            #endregion
        }

        public static bool TryDisconnect(Vector3Int posA, Vector3Int posB, int playerId, out string error)
        {
            // 接続対象を取得する
            // Acquire target chain poles
            error = string.Empty;
            if (!TryGetGearChainPole(posA, out var poleA, out var transformerA) || !TryGetGearChainPole(posB, out var poleB, out var transformerB))
            {
                error = "InvalidTarget";
                return false;
            }

            // 相互接続でない場合は失敗
            // Fail when not connected to each other
            if (!poleA.ContainsChainConnection(poleB.BlockInstanceId) || !poleB.ContainsChainConnection(poleA.BlockInstanceId))
            {
                error = "NotConnected";
                return false;
            }

            // 切断し、アイテムを返却
            // Disconnect and refund items
            poleA.TryRemoveChainConnection(poleB.BlockInstanceId, out var cost);
            poleB.TryRemoveChainConnection(poleA.BlockInstanceId, out _);
            
            RefundConsumption(cost, playerId);
            
            RebuildNetworks(transformerA, transformerB);
            
            return true;
            
            #region Internal
            
            void RefundConsumption(GearChainConnectionCost connectionCost, int player)
            {
                // 消費したアイテムをインベントリへ戻す
                // Return consumed items to inventory
                var inventory = ServerContext.GetService<IPlayerInventoryDataStore>().GetInventoryData(player).MainOpenableInventory;
                
                var remainder = inventory.InsertItem(connectionCost.ItemId, connectionCost.Count);
                
                if (remainder.Count <= 0) return;
                inventory.InsertItem(remainder);
            }
            
            #endregion
        }


        private static bool TryGetGearChainPole(Vector3Int position, out IGearChainPole chainPole, out IGearEnergyTransformer transformer)
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
