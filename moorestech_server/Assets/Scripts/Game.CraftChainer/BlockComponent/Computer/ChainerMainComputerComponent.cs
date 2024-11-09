using System.Collections.Generic;
using Core.Master;
using Game.Block.Component;
using Game.Block.Interface.Component;
using Game.CraftChainer.CraftChain;
using Game.CraftChainer.CraftNetwork;
using Newtonsoft.Json;

namespace Game.CraftChainer.BlockComponent.Computer
{
    public class ChainerMainComputerComponent : ICraftChainerNode
    {
        public readonly ChainerNetworkContext ChainerNetworkContext;
        
        public CraftChainerNodeId NodeId { get; } = CraftChainerNodeId.Create();
        
        public ChainerMainComputerComponent(BlockConnectorComponent<IBlockInventory> mainComputerConnector)
        {
            ChainerNetworkContext = new ChainerNetworkContext(mainComputerConnector, this);
        }
        
        public ChainerMainComputerComponent(Dictionary<string, string> componentStates, BlockConnectorComponent<IBlockInventory> mainComputerConnector) : this(mainComputerConnector)
        {
            var state = componentStates[SaveKey];
            var jsonObject = JsonConvert.DeserializeObject<ChainerMainComputerComponentJsonObject>(state);
            NodeId = new CraftChainerNodeId(jsonObject.NodeId);
        }
        
        public void StartCreateItem(ItemId itemId, int count)
        {
            var (recipes, initialInventory, targetItem) = CreateInitialData();
            
            var solverResult = CraftingSolver.Solve(recipes, initialInventory, targetItem);
            
            // アイテムは作成できなかった
            // The item could not be created
            if (solverResult == null)
            {
                return;
            }
            
            ChainerNetworkContext.SetCraftChainRecipeQue(solverResult);
            
            #region Internal
            
            (List<CraftingSolverRecipe> recipes, Dictionary<ItemId, int> initialInventory, CraftingSolverItem targetItem) CreateInitialData()
            {
                var recipeResults = new List<CraftingSolverRecipe>();
                foreach (var crafterComponent in ChainerNetworkContext.CrafterComponents)
                {
                    recipeResults.Add(crafterComponent.CraftingSolverRecipe);
                }
                
                var initialInventoryResults = new Dictionary<ItemId, int>();
                foreach (var chest in ChainerNetworkContext.ProviderChests)
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
        public string SaveKey { get; } = typeof(ChainerMainComputerComponent).FullName;
        public string GetSaveState()
        {
            return JsonConvert.SerializeObject(new ChainerMainComputerComponentJsonObject(this));
        }
    }
    
    public class ChainerMainComputerComponentJsonObject
    {
        [JsonProperty("nodeId")] public int NodeId { get; set; }
        
        public ChainerMainComputerComponentJsonObject(ChainerMainComputerComponent component)
        {
            NodeId = component.NodeId.AsPrimitive();
        }
    }
}