using System.Collections.Generic;
using Game.Block.Interface.Component;
using Game.CraftChainer.CraftChain;
using Game.CraftChainer.CraftNetwork;
using MessagePack;
using Newtonsoft.Json;

namespace Game.CraftChainer.BlockComponent.Crafter
{
    public class CraftCraftChainerCrafterComponent : ICraftChainerNode, IBlockStateDetail
    {
        public CraftChainerNodeId NodeId { get; } = CraftChainerNodeId.Create();
        
        public CraftingSolverRecipe CraftingSolverRecipe { get; private set; }
        
        public CraftCraftChainerCrafterComponent() { }
        
        public CraftCraftChainerCrafterComponent(Dictionary<string, string> componentStates) : this()
        {
            var state = componentStates[SaveKey];
            var jsonObject = JsonConvert.DeserializeObject<ChainerCrafterComponentSerializeObject>(state);
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
        
        public BlockStateDetail GetBlockStateDetail()
        {
            var bytes = MessagePackSerializer.Serialize(new ChainerCrafterComponentSerializeObject(this));
            return new BlockStateDetail(ChainerCrafterComponentSerializeObject.StateDetailKey, bytes);
        }
        
        public string SaveKey { get; } = typeof(CraftCraftChainerCrafterComponent).FullName;
        public string GetSaveState()
        {
            return JsonConvert.SerializeObject(new ChainerCrafterComponentSerializeObject(this));
        }
    }
    
    [JsonObject, MessagePackObject]
    public class ChainerCrafterComponentSerializeObject
    {
        public const string StateDetailKey = "ChainerCrafterComponent";
        
        [JsonProperty("recipe"), Key(0)] public CraftingSolverRecipeSerializeObject Recipe { get; set; }
        [JsonProperty("nodeId"), Key(1)] public int NodeId { get; set; }
        
        public ChainerCrafterComponentSerializeObject(){}
        public ChainerCrafterComponentSerializeObject(CraftCraftChainerCrafterComponent component)
        {
            Recipe = new CraftingSolverRecipeSerializeObject(component.CraftingSolverRecipe);
            NodeId = component.NodeId.AsPrimitive();
        }
    }
}