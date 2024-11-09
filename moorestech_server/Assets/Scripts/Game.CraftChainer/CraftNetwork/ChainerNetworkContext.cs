using System;
using System.Collections.Generic;
using Core.Item.Interface;
using Game.Block.Component;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;

namespace Game.CraftChainer.CraftNetwork
{
    public class ChainerNetworkContext
    {
        
        /// <summary>
        /// アイテムのIDとつながっているコネクターから、次にインサートすべきブロックを取得する
        /// Get the next block to insert from the connector connected to the item ID
        /// </summary>
        public IBlockInventory GetTransportNextBlock(ItemInstanceId item, CraftChainerNodeId craftChainerNodeId, BlockConnectorComponent<IBlockInventory> blockConnector)
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
                
                searchQueue.Enqueue(craftChainerNodeId);
                searched.Add(craftChainerNodeId);
                idToConnector[craftChainerNodeId] = (blockConnector, null);
                stepLog[craftChainerNodeId] = 0;
                
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
                        
                        reverseSearch[nodeId] = searchingId;
                        idToConnector[nodeId] = (nextConnector, blockInventory);
                        stepLog[nodeId] = step;
                        searchQueue.Enqueue(nodeId);
                        searched.Add(nodeId);
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
                while (current != craftChainerNodeId)
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
                if (node.NodeId == craftChainerNodeId) return null;
                if (!block.TryGetComponent<BlockConnectorComponent<IBlockInventory>>(out var connector)) return null;
                if (!block.TryGetComponent<IBlockInventory>(out var inventory)) return null;
                
                return (node.NodeId, connector, inventory);
            }
            
  #endregion
        }
        
        
        
        public CraftChainerNodeId GetTargetNodeId(ItemInstanceId item)
        {
            throw new NotImplementedException();
        }
        
    }
}