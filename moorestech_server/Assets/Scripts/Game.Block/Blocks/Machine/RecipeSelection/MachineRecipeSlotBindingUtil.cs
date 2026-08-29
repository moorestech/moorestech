using Core.Master;
using Mooresmaster.Model.MachineRecipesModule;

namespace Game.Block.Blocks.Machine.RecipeSelection
{
    /// <summary>
    ///     選択レシピとスロット番号の対応（入力スロットi＝素材i、出力スロットj＝生産物j）を引く純関数
    ///     Pure lookups for the recipe-to-slot binding (input slot i = input i, output slot j = output j)
    /// </summary>
    internal static class MachineRecipeSlotBindingUtil
    {
        // 素材のスロット番号。レシピに無いアイテムは-1
        // Input slot index for the item; -1 when the recipe does not use it
        public static int FindInputSlotIndex(MachineRecipeMasterElement recipe, ItemId itemId)
        {
            for (var i = 0; i < recipe.InputItems.Length; i++)
            {
                if (MasterHolder.ItemMaster.GetItemId(recipe.InputItems[i].ItemGuid) == itemId) return i;
            }
            return -1;
        }

        // 実現出力k（追加セット込み）の出力スロット番号。品質変種でIDが変わるため番号で引く
        // Output slot for realized output k (extra sets included); indexed, since quality variants change the id
        public static int FindOutputSlotIndex(MachineRecipeMasterElement recipe, int realizedOutputIndex)
        {
            return realizedOutputIndex % recipe.OutputItems.Length;
        }

        // 入力タンクiが受け入れる液体か
        // Whether input tank i accepts the fluid
        public static bool IsInputFluidBoundTo(MachineRecipeMasterElement recipe, int tankIndex, FluidId fluidId)
        {
            if (tankIndex < 0 || recipe.InputFluids.Length <= tankIndex) return false;
            return MasterHolder.FluidMaster.GetFluidId(recipe.InputFluids[tankIndex].FluidGuid) == fluidId;
        }

        // 生産物のレベルファミリー（品質モジュールの変種）に属するアイテムか
        // Whether the item belongs to the output's level family (quality module variants)
        public static bool IsOutputVariantOf(ItemId baseItemId, ItemId itemId)
        {
            foreach (var variant in MasterHolder.ItemMaster.GetLevelVariants(baseItemId))
            {
                if (variant == itemId) return true;
            }
            return false;
        }
    }
}
