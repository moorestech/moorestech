using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Mooresmaster.Loader.MachineRecipesModule;
using Mooresmaster.Model.MachineRecipesModule;
using Newtonsoft.Json.Linq;

namespace Core.Master
{
    public class MachineRecipesMaster
    {
        public readonly MachineRecipes MachineRecipes; // TODO 個々の使用箇所をメソッドか
        private readonly Dictionary<string, MachineRecipeMasterElement> _machineRecipesByRecipeKey;
        
        public MachineRecipesMaster(JToken jToken)
        {
            MachineRecipes = MachineRecipesLoader.Load(jToken);
            
            _machineRecipesByRecipeKey = new Dictionary<string, MachineRecipeMasterElement>();
            BuildMachineRecipes();
            
            #region Internal
            
            void BuildMachineRecipes()
            {
                foreach (var recipe in MachineRecipes.Data)
                {
                    var inputItemIds = new List<ItemId>();
                    foreach (var inputItem in recipe.InputItems)
                    {
                        var itemId = MasterHolder.ItemMaster.GetItemId(inputItem.ItemGuid);
                        inputItemIds.Add(itemId);
                    }

                    var fluidInputIds = new List<FluidId>();
                    foreach (var inputFluid in recipe.InputFluids)
                    {
                        var fluidId = MasterHolder.FluidMaster.GetFluidId(inputFluid.FluidGuid);
                        fluidInputIds.Add(fluidId);
                    }

                    var blockId = MasterHolder.BlockMaster.GetBlockId(recipe.BlockGuid);
                    
                    var key = GetRecipeElementKey(blockId, inputItemIds, fluidInputIds);
                    _machineRecipesByRecipeKey.Add(key, recipe);
                }
            }
            
            #endregion
        }
        
        public bool TryGetRecipeElement(BlockId blockId, List<ItemId> inputItemIds, List<FluidId> fluidInputIds, out MachineRecipeMasterElement recipe)
        {
            var key = GetRecipeElementKey(blockId, inputItemIds, fluidInputIds);
            return _machineRecipesByRecipeKey.TryGetValue(key, out recipe);
        }
        
        public MachineRecipeMasterElement GetRecipeElement(Guid machineRecipeGuid)
        {
            return MachineRecipes.Data.ToList().Find(x => x.MachineRecipeGuid == machineRecipeGuid);
        }
        
        private static string GetRecipeElementKey(BlockId blockId, List<ItemId> itemIds, List<FluidId> fluidIds)
        {
            StringBuilder key = new StringBuilder();
            key.Append(blockId);
            
            itemIds.Sort((a, b) => a.AsPrimitive() - b.AsPrimitive());
            itemIds.ForEach(i =>
            {
                key.Append('_');
                key.Append(i.AsPrimitive());
            });
            
            fluidIds.Sort((a, b) => a.AsPrimitive() - b.AsPrimitive());
            fluidIds.ForEach(i =>
            {
                key.Append('_');
                key.Append(i.AsPrimitive());
            });
            
            return key.ToString();
        }
    }
    
    public static class MachineRecipeMasterExtension
    {
        public static ItemId GetBlockItemId(this MachineRecipeMasterElement recipe)
        {
            var blockId = MasterHolder.BlockMaster.GetBlockId(recipe.BlockGuid);
            return MasterHolder.BlockMaster.GetItemId(blockId);
        }
    }
}