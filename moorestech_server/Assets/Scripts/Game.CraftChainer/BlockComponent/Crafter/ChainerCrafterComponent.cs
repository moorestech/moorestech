using System;
using System.Collections.Generic;
using Game.Block.Interface.Component;
using Game.CraftChainer.CraftChain;
using Game.CraftChainer.CraftNetwork;
using Newtonsoft.Json;

namespace Game.CraftChainer.BlockComponent.Crafter
{
    public class ChainerCrafterComponent : IBlockSaveState, ICraftChainerNode
    {
        public CraftChainerNodeId NodeId { get; } = CraftChainerNodeId.Create();
        
        public CraftingSolverRecipe CraftingSolverRecipe { get; private set; }
        
        public ChainerCrafterComponent()
        {
        }
        
        public ChainerCrafterComponent(Dictionary<string, string> componentStates) : this()
        {
            var state = componentStates[SaveKey];
            var jsonObject = JsonConvert.DeserializeObject<ChainerCrafterComponentJsonObject>(state);
            CraftingSolverRecipe = jsonObject.Recipe.ToCraftingSolverRecipe();
            NodeId = new CraftChainerNodeId(jsonObject.NodeId);
        }
        
        public void SetRecipe(List<CraftingSolverItem> inputItems, List<CraftingSolverItem> outputItem)
        {
            var id = CraftingSolverRecipeId.Create();
            CraftingSolverRecipe = new CraftingSolverRecipe(id, inputItems, outputItem);
        }
        
        
        public bool IsDestroy { get; private set; }
        public void Destroy()
        {
            IsDestroy = true;
        }
        
        public string SaveKey { get; } = typeof(ChainerCrafterComponent).FullName;
        public string GetSaveState()
        {
            return JsonConvert.SerializeObject(new ChainerCrafterComponentJsonObject(this));
        }
    }
    
    public class ChainerCrafterComponentJsonObject
    {
        [JsonProperty("recipe")] public CraftingSolverRecipeJsonObject Recipe { get; set; }
        [JsonProperty("nodeId")] public int NodeId { get; set; }
        
        public ChainerCrafterComponentJsonObject(ChainerCrafterComponent component)
        {
            Recipe = new CraftingSolverRecipeJsonObject(component.CraftingSolverRecipe);
            NodeId = component.NodeId.AsPrimitive();
        }
    }
}