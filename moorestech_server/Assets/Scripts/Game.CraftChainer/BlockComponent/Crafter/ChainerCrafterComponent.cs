using System.Collections.Generic;
using Game.Block.Interface.Component;
using Game.CraftChainer.CraftChain;
using Game.CraftChainer.CraftNetwork;
using Newtonsoft.Json;

namespace Game.CraftChainer.BlockComponent.Crafter
{
    public class ChainerCrafterComponent : IBlockSaveState
    {
        public CraftingSolverRecipe CraftingSolverRecipe { get; private set; }
        
        public readonly CraftChainerNodeId CraftChainerNodeId;
        
        public ChainerCrafterComponent()
        {
        }
        
        public ChainerCrafterComponent(string state) : this()
        {
            var jsonObject = JsonConvert.DeserializeObject<CraftingSolverRecipeJsonObject>(state);
            CraftingSolverRecipe = jsonObject.ToCraftingSolverRecipe();
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
        public string GetSaveState()
        {
            return JsonConvert.SerializeObject(new CraftingSolverRecipeJsonObject(CraftingSolverRecipe));
        }
    }
}