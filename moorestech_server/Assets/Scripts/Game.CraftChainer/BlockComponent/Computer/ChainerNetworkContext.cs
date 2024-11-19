using System.Collections.Generic;
using Core.Item.Interface;
using Core.Master;
using Game.Block.Component;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.CraftChainer.BlockComponent.Crafter;
using Game.CraftChainer.BlockComponent.ProviderChest;
using Game.CraftChainer.CraftChain;
using Game.CraftChainer.CraftNetwork;
using UnityEngine;

namespace Game.CraftChainer.BlockComponent.Computer
{
    public class ChainerNetworkContext
    {
        // チェインネットワークに関する情報
        // Information about the chain network
        public IReadOnlyList<ChainerProviderChestComponent> ProviderChests => _providerChests;
        private readonly List<ChainerProviderChestComponent> _providerChests = new();
        public IReadOnlyList<ChainerCrafterComponent> CrafterComponents => _crafterComponents;
        private readonly List<ChainerCrafterComponent> _crafterComponents = new();
        private readonly Dictionary<CraftChainerNodeId, ICraftChainerNode> _nodes = new();
        
        // このコンテキストを保持するメインコンピューターの情報
        // Information about the main computer that holds this context
        private readonly BlockConnectorComponent<IBlockInventory> _mainComputerConnector; 
        private readonly ICraftChainerNode _mainComputerNode;
        
        // 現在クラフト中のアイテム情報
        // Information about the item currently being crafted
        private readonly Dictionary<ItemInstanceId,CraftChainerNodeId> _requestedMoveItems = new();
        // アイテムごとにどのノードに何個アイテムを入れなければならないか
        // How many items of each item type must be placed in each node?
        private Dictionary<ItemId,Dictionary<CraftChainerNodeId,int>> _craftChainRecipeQue;
        
        public ChainerNetworkContext(BlockConnectorComponent<IBlockInventory> mainComputerConnector, ICraftChainerNode mainComputerNode)
        {
            _mainComputerConnector = mainComputerConnector;
            _mainComputerNode = mainComputerNode;
        }
        
        public bool IsExistNode(CraftChainerNodeId nodeId)
        {
            return _nodes.ContainsKey(nodeId);
        }
        
        public void SetCraftChainRecipeQue(Dictionary<CraftingSolverRecipeId, int> solvedResults, CraftingSolverItem targetItem)
        {
            var craftRecipeIdMap = GetCraftRecipeIdMap();
            
            _craftChainRecipeQue = CreateRecipeQue(solvedResults, craftRecipeIdMap);
            // ターゲットのアイテムをコンピューターに移動するようにリクエストを追加しておく
            _craftChainRecipeQue.Add(targetItem.ItemId, new Dictionary<CraftChainerNodeId, int> {{_mainComputerNode.NodeId, targetItem.Quantity}});
            
            foreach (var ques in _craftChainRecipeQue)
            {
                foreach (var que in ques.Value)
                {
                    Debug.Log($"Set Id {ques.Key} Count {que.Value} TargetId {que.Key}");
                }
            }
            
            #region Internal
            
            Dictionary<CraftingSolverRecipeId, ChainerCrafterComponent> GetCraftRecipeIdMap()
            {
                var map = new Dictionary<CraftingSolverRecipeId, ChainerCrafterComponent>();
                foreach (var crafter in _crafterComponents)
                {
                    map[crafter.CraftingSolverRecipe.CraftingSolverRecipeId] = crafter;
                }
                return map;
            }
            
            Dictionary<ItemId, Dictionary<CraftChainerNodeId,int>> CreateRecipeQue(Dictionary<CraftingSolverRecipeId, int> solved, Dictionary<CraftingSolverRecipeId, ChainerCrafterComponent> recipes)
            {
                var result = new Dictionary<ItemId, Dictionary<CraftChainerNodeId,int>>();
                foreach (var solvedResult in solved)
                {
                    var crafter = recipes[solvedResult.Key];
                    var recipe = crafter.CraftingSolverRecipe;
                    foreach (var inputItems in recipe.Inputs)
                    {
                        var quantity = inputItems.Quantity * solvedResult.Value;
                        if (result.TryGetValue(inputItems.ItemId, out var que))
                        {
                            if (!que.TryAdd(crafter.NodeId,quantity))
                            {
                                que[crafter.NodeId] += quantity;
                            }
                        }
                        else
                        {
                            result[inputItems.ItemId] = new Dictionary<CraftChainerNodeId, int>()
                            {
                                { crafter.NodeId, quantity }
                            };
                        }
                    }
                }
                
                return result;
            }
            
            #endregion
        }
        
        /// <summary>
        /// クラフトチェインのネットワークを再検索する
        /// Re-search the network of the craft chain
        /// </summary>
        public void ReSearchNetwork()
        {
            _providerChests.Clear();
            _nodes.Clear();
            
            _nodes.Add(_mainComputerNode.NodeId, _mainComputerNode);
            
            // 単純に深さ優先探索で探索し、途中にあったチェストをリストに追加
            // Simply search by depth-first search and add the chests found on the way to the list
            Search(_mainComputerConnector);
            
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
                    if (!_nodes.TryAdd(node.NodeId, node))
                    {
                        continue;
                    }
                    
                    if (targetBlock.TryGetComponent<ChainerProviderChestComponent>(out var chest))
                    {
                        _providerChests.Add(chest);
                    }
                    if (targetBlock.TryGetComponent<ChainerCrafterComponent>(out var crafter))
                    {
                        _crafterComponents.Add(crafter);
                    }
                    if (targetBlock.TryGetComponent<BlockConnectorComponent<IBlockInventory>>(out var nextConnector))
                    {
                        Search(nextConnector);
                    }
                }
            }
            
            #endregion
        }
        
        // DEBUG 消す
        private void ExportCraftChainRecipeQueLog()
        {
            var str = "";
            
            foreach (var ques in _craftChainRecipeQue)
            {
                foreach (var que in ques.Value)
                {
                    var crafter = _crafterComponents.Find(c => c.NodeId == que.Key);
                    if (crafter != null)
                    {
                        var outputId = crafter.CraftingSolverRecipe.Outputs[0].ItemId;
                        var outItemName = MasterHolder.ItemMaster.GetItemMaster(outputId).Name;
                        var itemName = MasterHolder.ItemMaster.GetItemMaster(ques.Key).Name;
                        
                        str += $"Id {itemName} Count {que.Value} Output {outItemName},  ";
                    }
                }
            }
            
            Debug.Log(str);
        }
        
        // DEBUG 消す
        public static List<(CraftChainerNodeId, IBlockInventory)> Result;
        
        /// <summary>
        /// アイテムのIDとつながっているコネクターから、次にインサートすべきブロックを取得する
        /// modifyCraftCacheのフラグすごい良くない感じはしているのだが、メソッドを分けるよりかはこれが一番コンテキストが浅いので、一旦これで、、
        /// TODO もしかしたらこの中でinsertを行って、その結果だけ返すほうがいい説あるかもしれない？要検討
        /// ↑というかそうすべきでは
        /// Get the next block to insert from the connector connected to the item ID
        /// </summary>
        public IBlockInventory GetTransportNextBlock(bool modifyCraftCacheData, IItemStack item, CraftChainerNodeId startChainerNodeId, BlockConnectorComponent<IBlockInventory> blockConnector)
        {
            var targetNodeId = GetTargetNodeId(item);
            if (targetNodeId == CraftChainerNodeId.Invalid)
            {
                return null;
            }
            
            var result = ExecuteBfs(targetNodeId);
            if (result == null || result.Count == 0)
            {
                return null;
            }
            
            Result = result;
            return result[0].Item2;
            
            #region Internal
            
            CraftChainerNodeId GetTargetNodeId(IItemStack item)
            {
                // 移動先が既に指定されている場合はそのまま返す
                // If the destination is already specified, return it as it is
                if (_requestedMoveItems.TryGetValue(item.ItemInstanceId, out var nodeId))
                {
                    return nodeId;
                }
                
                // 現在のアイテムがクラフト対象の材料だったら
                // If the current item is a crafting target material
                if (!_craftChainRecipeQue.TryGetValue(item.Id, out var craftQue))
                {
                    return CraftChainerNodeId.Invalid;
                }
                
                
                foreach (var nodeReminder in craftQue)
                {
                    var keyNodeId = nodeReminder.Key;
                    
                    // データを変更する
                    // Modify the data
                    if (modifyCraftCacheData)
                    {
                        ExportCraftChainRecipeQueLog();
                        
                        var reminder = nodeReminder.Value;
                        reminder--;
                        if (reminder <= 0)
                        {
                            craftQue.Remove(keyNodeId);
                        }
                        else
                        {
                            craftQue[keyNodeId] = reminder;
                        }
                        
                        // 計算したアイテムの移動先を保持
                        // Keep the destination of the calculated item
                        _requestedMoveItems[item.ItemInstanceId] = keyNodeId;
                    }
                    
                    return keyNodeId;
                }
                
                // 移動先が特に指定されていない場合はIncalidを返す
                // If no destination is specified, select randomly
                return CraftChainerNodeId.Invalid;
            }
            
            List<(CraftChainerNodeId,IBlockInventory)> ExecuteBfs(CraftChainerNodeId targetNode)
            {
                var idToConnector = new Dictionary<CraftChainerNodeId, (BlockConnectorComponent<IBlockInventory> connector, IBlockInventory blockInventory)>();
                var searchQueue = new Queue<CraftChainerNodeId>();
                var searched = new HashSet<CraftChainerNodeId>();
                var reverseSearch = new Dictionary<CraftChainerNodeId, CraftChainerNodeId>();
                var stepLog = new Dictionary<CraftChainerNodeId, int>();
                var isFound = false;
                
                searchQueue.Enqueue(startChainerNodeId);
                searched.Add(startChainerNodeId);
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
                var result = new List<(CraftChainerNodeId,IBlockInventory)>();
                var current = targetNode;
                while (current != startChainerNodeId)
                {
                    result.Add((current,idToConnector[current].blockInventory));
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
        
        class RecipeQueInfo
        {
            public CraftChainerNodeId TargetNodeId { get; }
            public int ReminderCount { get; set; }
            
            public RecipeQueInfo(CraftChainerNodeId targetNodeId)
            {
                TargetNodeId = targetNodeId;
            }
        }
    }
}