using System.Collections.Generic;
using Core.Master;
using Game.Block.Component;
using Game.Block.Interface.Component;
using Game.CraftChainer.CraftChain;
using Game.CraftChainer.CraftNetwork;
using Newtonsoft.Json;

namespace Game.CraftChainer.BlockComponent.Computer
{
    public class CraftChainerMainComputerComponent : ICraftChainerNode
    {
        public readonly CraftChainerNetworkContext CraftChainerNetworkContext;
        
        public CraftChainerNodeId NodeId { get; } = CraftChainerNodeId.Create();
        
        public CraftChainerMainComputerComponent(BlockConnectorComponent<IBlockInventory> mainComputerConnector)
        {
            CraftChainerNetworkContext = new CraftChainerNetworkContext(mainComputerConnector, this);
        }
        
        public CraftChainerMainComputerComponent(Dictionary<string, string> componentStates, BlockConnectorComponent<IBlockInventory> mainComputerConnector) : this(mainComputerConnector)
        {
            var state = componentStates[SaveKey];
            var jsonObject = JsonConvert.DeserializeObject<ChainerMainComputerComponentJsonObject>(state);
            NodeId = new CraftChainerNodeId(jsonObject.NodeId);
        }
        
        /// <summary>
        /// アイテムのクラフトをリクエストする
        /// Request to create an item
        /// </summary>
        /// <returns>
        /// クラフトリクエストが成功したかどうか
        /// Whether the craft request was successful
        /// </returns>
        public bool StartCreateItem(ItemId itemId, int count)
        {
            var (recipes, initialInventory, targetItem) = CreateInitialData();
            
            var solverResult = CraftChainerCraftingSolver.Solve(recipes, initialInventory, targetItem);
            
            // アイテムは作成できなかった
            // The item could not be created
            if (solverResult == null)
            {
                return false;
            }
            
            CraftChainerNetworkContext.SetCraftChainRecipeQue(solverResult, targetItem);
            return true;
            
            #region Internal
            
            (List<CraftingSolverRecipe> recipes, Dictionary<ItemId, int> initialInventory, CraftingSolverItem targetItem) CreateInitialData()
            {
                var recipeResults = new List<CraftingSolverRecipe>();
                foreach (var crafterComponent in CraftChainerNetworkContext.CrafterComponents)
                {
                    recipeResults.Add(crafterComponent.CraftingSolverRecipe);
                }
                
                var initialInventoryResults = new Dictionary<ItemId, int>();
                foreach (var chest in CraftChainerNetworkContext.ProviderChests)
                {
                    foreach (var item in chest.Inventory)
                    {
                        if (initialInventoryResults.ContainsKey(item.Id))
                        {
                            initialInventoryResults[item.Id] += item.Count;
                        }
                        else
                        {
                            initialInventoryResults[item.Id] = item.Count;
                        }
                    }
                }
                
                var target = new CraftingSolverItem(itemId, count);
                
                return (recipeResults, initialInventoryResults, target);
            }
            
  #endregion
        }
        
        
        public bool IsDestroy { get; private set; }
        public void Destroy()
        {
            IsDestroy = true;
        }
        public string SaveKey { get; } = typeof(CraftChainerMainComputerComponent).FullName;
        public string GetSaveState()
        {
            return JsonConvert.SerializeObject(new ChainerMainComputerComponentJsonObject(this));
        }
    }
    
    public class ChainerMainComputerComponentJsonObject
    {
        [JsonProperty("nodeId")] public int NodeId { get; set; }
        
        public ChainerMainComputerComponentJsonObject(){}
        public ChainerMainComputerComponentJsonObject(CraftChainerMainComputerComponent component)
        {
            NodeId = component.NodeId.AsPrimitive();
        }
    }
}