using System.Collections.Generic;
using Core.Master;
using Mooresmaster.Model.MachineRecipesModule;

namespace Game.Block.Blocks.Machine.RecipeSelection
{
    /// <summary>
    ///     選択レシピとスロット番号の対応（入力スロットi＝素材i、出力スロットj＝生産物jのレベルファミリー枠）を引く純関数
    ///     Pure lookups for the recipe-to-slot binding (input slot i = input i, output slot j = output j's level-family frame)
    /// </summary>
    internal static class MachineRecipeSlotBindingUtil
    {
        // スロットslotIndexが素材itemIdの束縛先か
        // Whether slot slotIndex is bound to input itemId
        public static bool IsInputBoundTo(MachineRecipeMasterElement recipe, int slotIndex, ItemId itemId)
        {
            if (slotIndex < 0 || recipe.InputItems.Length <= slotIndex) return false;
            return MasterHolder.ItemMaster.GetItemId(recipe.InputItems[slotIndex].ItemGuid) == itemId;
        }

        // 入力タンクiが受け入れる液体か
        // Whether input tank i accepts the fluid
        public static bool IsInputFluidBoundTo(MachineRecipeMasterElement recipe, int tankIndex, FluidId fluidId)
        {
            if (tankIndex < 0 || recipe.InputFluids.Length <= tankIndex) return false;
            return MasterHolder.FluidMaster.GetFluidId(recipe.InputFluids[tankIndex].FluidGuid) == fluidId;
        }

        // 出力スロットごとの既定許可集合（生産物のレベルファミリーのみ）。レシピ未選択は空
        // Default allowed-item set per output slot (level family only); empty when unselected
        public static IReadOnlyList<IReadOnlyCollection<ItemId>> BuildDefaultOutputBinding(MachineRecipeMasterElement recipe)
        {
            if (recipe == null) return System.Array.Empty<IReadOnlyCollection<ItemId>>();

            var binding = new List<IReadOnlyCollection<ItemId>>(recipe.OutputItems.Length);
            foreach (var outputItem in recipe.OutputItems)
            {
                var baseItemId = MasterHolder.ItemMaster.GetItemId(outputItem.ItemGuid);
                binding.Add(new HashSet<ItemId>(MasterHolder.ItemMaster.GetLevelVariants(baseItemId)));
            }
            return binding;
        }
    }
}
