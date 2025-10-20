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
                    var inputItemIds = new List<ItemId>(recipe.InputItems.Length);
                    foreach (var inputItem in recipe.InputItems)
                    {
                        var itemId = MasterHolder.ItemMaster.GetItemId(inputItem.ItemGuid);
                        inputItemIds.Add(itemId);
                    }
                    var fluidIds = new List<FluidId>(recipe.InputFluids.Length);
                    foreach (var inputFluid in recipe.InputFluids)
                    {
                        var fluidId = MasterHolder.FluidMaster.GetFluidId(inputFluid.FluidGuid);
                        fluidIds.Add(fluidId);
                    }
                    
                    var blockId = MasterHolder.BlockMaster.GetBlockId(recipe.BlockGuid);
                    
                    var key = GetRecipeElementKey(blockId, inputItemIds, fluidIds);
                    
                    if (!_machineRecipesByRecipeKey.TryAdd(key, recipe))
                    {
                        var blockMaster = MasterHolder.BlockMaster.GetBlockMaster(recipe.BlockGuid);
                        var inputItemMaster = inputItemIds
                            .Select(i => MasterHolder.ItemMaster.GetItemMaster(i))
                            .ToList();
                        
                        var recipe1Guid = _machineRecipesByRecipeKey[key].MachineRecipeGuid;
                        var recipe2Guid = recipe.MachineRecipeGuid;
                        var recipe1Index = MachineRecipes.Data.ToList().FindIndex(x => x.MachineRecipeGuid == recipe1Guid);
                        var recipe2Index = MachineRecipes.Data.ToList().FindIndex(x => x.MachineRecipeGuid == recipe2Guid);
                        
                        throw new Exception($"機械レシピマスタに同じレシピが登録されています。ブロックID:{blockMaster.Name} アイテムIDリスト: {string.Join(", ", inputItemMaster.Select(i => i.Name))} \n" +
                                            $"レシピIndex1: {recipe1Index} レシピIndex2: {recipe2Index} \n" +
                                            $"レシピID1: {recipe1Guid} レシピID2: {recipe2Guid}");
                    }
                }
            }
            
            #endregion
        }
        
        public bool TryGetRecipeElement(BlockId blockId, List<ItemId> inputItemIds, List<FluidId> inputFluids, out MachineRecipeMasterElement recipe)
        {
            var key = GetRecipeElementKey(blockId, inputItemIds, inputFluids);
            return _machineRecipesByRecipeKey.TryGetValue(key, out recipe);
        }
        
        public MachineRecipeMasterElement GetRecipeElement(Guid machineRecipeGuid)
        {
            return MachineRecipes.Data.ToList().Find(x => x.MachineRecipeGuid == machineRecipeGuid);
        }
        
        private static string GetRecipeElementKey(BlockId blockId, List<ItemId> itemIds, List<FluidId> fluidIds)
        {
            StringBuilder items = new StringBuilder();
            items.Append(blockId);
            
            itemIds.Sort((a, b) => a.AsPrimitive() - b.AsPrimitive());
            for (var i = 0; i < itemIds.Count; i++)
            {
                items.Append('_');
                items.Append(itemIds[i].AsPrimitive());
            }
            
            items.Append('|');
            for (var i = 0; i < fluidIds.Count; i++)
            {
                if (i > 0) items.Append('_');
                items.Append(fluidIds[i].AsPrimitive());
            }
            
            return items.ToString();
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