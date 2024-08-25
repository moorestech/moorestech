using System.Collections.Generic;
using System.Linq;
using System.Text;
using Core.Item.Interface;
using Game.Context;
using Mooresmaster.Model.MachineRecipesModule;

namespace Core.Master
{
    public static class MachineRecipeMasterUtil
    {
        private static Dictionary<string, MachineRecipeElement> _machineRecipes;
        
        public static bool TryGetRecipeElement(BlockId blockId, IReadOnlyList<IItemStack> inputSlot,out MachineRecipeElement recipe)
        {
            if (_machineRecipes == null) BuildMachineRecipes();
            
            var tmpInputItem = inputSlot.Where(i => i.Count != 0).ToList();
            var key = GetRecipeElementKey(blockId, tmpInputItem);
            
            // ReSharper disable once PossibleNullReferenceException
            return _machineRecipes.TryGetValue(key, out recipe);
        }
        
        public static bool RecipeConfirmation(this MachineRecipeElement recipe,IReadOnlyList<IItemStack> inputSlot, BlockId blockId)
        {
            if (_machineRecipes == null) BuildMachineRecipes();
            var recipeBlockId = BlockMaster.GetBlockId(recipe.BlockGuid);
            if (recipeBlockId != blockId) return false;
            
            // アイテムが十分な数満たされている数が、必要とする数と一致するか
            var okCnt = 0;
            foreach (var slot in inputSlot)
            {
                var slotGuid = ItemMaster.GetItemMaster(slot.Id).ItemGuid;
                okCnt += recipe.InputItems.Count(input => slotGuid == input.ItemGuid && input.Count <= slot.Count);
            }
            
            return okCnt == recipe.InputItems.Length;
        }
        
        private static void BuildMachineRecipes()
        {
            _machineRecipes = new Dictionary<string, MachineRecipeElement>();
            foreach (var recipe in MasterHolder.MachineRecipes.Data)
            {
                var inputItemStacks = new List<IItemStack>();
                foreach (var inputItem in recipe.InputItems)
                {
                    var item = ServerContext.ItemStackFactory.Create(ItemMaster.GetItemId(inputItem.ItemGuid), inputItem.Count);
                    inputItemStacks.Add(item);
                }
                
                var blockId = BlockMaster.GetBlockId(recipe.BlockGuid);
                var key = GetRecipeElementKey(blockId, inputItemStacks);
                _machineRecipes.Add(key, recipe);
            }
        }
        
        private static string GetRecipeElementKey(BlockId blockId, List<IItemStack> itemIds)
        {
            StringBuilder items = new StringBuilder();
            items.Append(blockId);
            items.Append('_');
            
            itemIds.Sort((a, b) => a.Id.AsPrimitive() - b.Id.AsPrimitive());
            itemIds.ForEach(i =>
            {
                items.Append('_');
                items.Append(i.Id);
            });
            return items.ToString();
        }
    }
}