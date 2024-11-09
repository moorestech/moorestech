using System.Collections.Generic;
using Core.Item.Interface;
using Core.Master;
using Game.Block.Component;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.CraftChainer.BlockComponent.ProviderChest;
using Game.CraftChainer.CraftNetwork;
using UnityEngine;

namespace Game.CraftChainer.BlockComponent.Computer
{
    public class ChainerNetworkContext
    {
        private readonly HashSet<ICraftChainerNode> _nodes = new();
        private readonly List<ChainerProviderChestComponent> _providerChests = new();
        
        private Dictionary<ItemId,(CraftChainerNodeId targetNodeId, int reminderCount)> _craftChainRecipeQue;
        private Dictionary<ItemInstanceId,CraftChainerNodeId> _requestedMoveItems;
        
        public void ReSearchProviderChests(BlockConnectorComponent<IBlockInventory> startConnector)
        {
            _providerChests.Clear();
            _nodes.Clear();
            
            // 単純に深さ優先探索で探索し、途中にあったチェストをリストに追加
            // Simply search by depth-first search and add the chests found on the way to the list
            Search(startConnector);
            
            #region Internal
            
            void Search(BlockConnectorComponent<IBlockInventory> connector)
            {
                foreach (var connectedTarget in connector.ConnectedTargets)
                {
                    var targetBlock = connectedTarget.Value.TargetBlock;
                    if (!targetBlock.TryGetComponent<ICraftChainerNode>(out var node))
                    {
                        continue;
                    }
                    
                    _nodes.Add(node);
                    if (targetBlock.TryGetComponent<ChainerProviderChestComponent>(out var chest))
                    {
                        _providerChests.Add(chest);
                    }
                    if (targetBlock.TryGetComponent<BlockConnectorComponent<IBlockInventory>>(out var nextConnector))
                    {
                        Search(nextConnector);
                    }
                }
            }
            
            #endregion
        }
        
        public CraftChainerNodeId GetTargetNodeId(IItemStack item)
        {
            // 移動先が既に指定されている場合はそのまま返す
            // If the destination is already specified, return it as it is
            if (_requestedMoveItems.TryGetValue(item.ItemInstanceId, out var nodeId))
            {
                return nodeId;
            }
            
            // 現在のアイテムがクラフト対象の材料だったら
            // If the current item is a crafting target material
            if (_craftChainRecipeQue.TryGetValue(item.Id, out var craftQue))
            {
                var newCraftQue = craftQue;
                newCraftQue.reminderCount--;
                if (newCraftQue.reminderCount <= 0)
                {
                    _craftChainRecipeQue.Remove(item.Id);
                }
                else
                {
                    _craftChainRecipeQue[item.Id] = newCraftQue;
                }
                
                // 計算したアイテムの移動先を保持
                // Keep the destination of the calculated item
                _requestedMoveItems[item.ItemInstanceId] = newCraftQue.targetNodeId;
                return newCraftQue.targetNodeId;
            }
            
            // 移動先が特に指定されていない場合はランダムに選択
            // If no destination is specified, select randomly
            var randomProvider = _providerChests[Random.Range(0, _providerChests.Count)];
            if (randomProvider == null)
            {
                return CraftChainerNodeId.Invalid;
            }
            return randomProvider.NodeId;
        }
        
        
        /// <summary>
        /// アイテムのIDとつながっているコネクターから、次にインサートすべきブロックを取得する
        /// Get the next block to insert from the connector connected to the item ID
        /// </summary>
        public IBlockInventory GetTransportNextBlock(IItemStack item, CraftChainerNodeId startChainerNodeId, BlockConnectorComponent<IBlockInventory> blockConnector)
        {
            var targetNodeId = GetTargetNodeId(item);
            
            var result = ExecuteBfs(targetNodeId);
            if (result == null || result.Count == 0)
            {
                return null;
            }
            
            return result[0];
            
            #region Internal
            
            List<IBlockInventory> ExecuteBfs(CraftChainerNodeId targetNode)
            {
                var idToConnector = new Dictionary<CraftChainerNodeId, (BlockConnectorComponent<IBlockInventory> connector, IBlockInventory blockInventory)>();
                var searchQueue = new Queue<CraftChainerNodeId>();
                var searched = new HashSet<CraftChainerNodeId>();
                var reverseSearch = new Dictionary<CraftChainerNodeId, CraftChainerNodeId>();
                var stepLog = new Dictionary<CraftChainerNodeId, int>();
                var isFound = false;
                
                searchQueue.Enqueue(startChainerNodeId);
                searched.Add(startChainerNodeId); // Add starting node to searched
                idToConnector[startChainerNodeId] = (blockConnector, null);
                stepLog[startChainerNodeId] = 0;
                
                // キューがなくなるまでループ
                // Loop until the queue is empty
                while (0 < searchQueue.Count)
                {
                    var searchingId = searchQueue.Dequeue();
                    if (searchingId == targetNode)
                    {
                        isFound = true;
                        break;
                    }
                    
                    var step = stepLog[searchingId] + 1;
                    foreach (var connectedTarget in idToConnector[searchingId].connector.ConnectedTargets)
                    {
                        var targetBlock = connectedTarget.Value.TargetBlock;
                        var next = GetNext(targetBlock);
                        
                        // 接続先がChainerNodeではないので無視
                        // Ignore if the connection destination is not a ChainerNode
                        if (!next.HasValue) continue;
                        
                        var (nodeId, nextConnector, blockInventory) = next.Value;
                        
                        // すでに探索済みの場合は無視
                        // Ignore if already searched
                        if (searched.Contains(nodeId)) continue;
                        
                        searched.Add(nodeId); // Mark as searched before enqueuing
                        reverseSearch[nodeId] = searchingId;
                        idToConnector[nodeId] = (nextConnector, blockInventory);
                        stepLog[nodeId] = step;
                        searchQueue.Enqueue(nodeId);
                    }
                }
                
                if (!isFound)
                {
                    return null;
                }
                
                // 経路をたどっていく
                // Follow the path
                var result = new List<IBlockInventory>();
                var current = targetNode;
                while (current != startChainerNodeId)
                {
                    result.Add(idToConnector[current].blockInventory);
                    current = reverseSearch[current];
                }
                
                result.Reverse();
                return result;
            }
            
            (CraftChainerNodeId nodeId, BlockConnectorComponent<IBlockInventory> connector, IBlockInventory blockInventory)? GetNext(IBlock block)
            {
                if (!block.TryGetComponent<ICraftChainerNode>(out var node)) return null;
                if (node.NodeId == startChainerNodeId) return null;
                if (!block.TryGetComponent<BlockConnectorComponent<IBlockInventory>>(out var connector)) return null;
                if (!block.TryGetComponent<IBlockInventory>(out var inventory)) return null;
                
                return (node.NodeId, connector, inventory);
            }
            
            #endregion
        }
        
    }
}