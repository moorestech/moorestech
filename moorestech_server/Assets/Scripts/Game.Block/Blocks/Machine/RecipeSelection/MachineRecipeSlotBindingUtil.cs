using System.Collections.Generic;
using Core.Item;
using Core.Master;
using Mooresmaster.Model.MachineRecipesModule;

namespace Game.Block.Blocks.Machine.RecipeSelection
{
    /// <summary>
    ///     選択レシピとスロット番号の対応（入力スロットi＝素材i、出力スロットj＝生産物jのレベルファミリー枠）を引く純関数
    ///     Pure lookups for the recipe-to-slot binding (input slot i = input i, output slot j = output j's level-family frame)
    /// </summary>
    public static class MachineRecipeSlotBindingUtil
    {
        // 入力 inputIndex に充てられるアイテム（下位から順）。要求がファミリーの基準なら全変種（継承元スロットも同じ規則。製作上限は別のゲートが見る）
        // Items that can serve input inputIndex, lowest first; every variant for a family-base requirement (the source slot follows the same rule; the craft cap is a separate gate)
        public static IReadOnlyList<ItemId> GetAcceptedInputItemIds(MachineRecipeMasterElement recipe, int inputIndex)
        {
            return LevelFamilySubstituteUtil.GetAcceptedItemIds(GetRequiredInputItemId(recipe, inputIndex));
        }

        // 入力 inputIndex に itemId を充てたときのレベル（1始まり）。充てられなければ null
        // Level (1-based) when itemId serves input inputIndex; null when it cannot
        public static int? ResolveInputAcceptedLevel(MachineRecipeMasterElement recipe, int inputIndex, ItemId itemId)
        {
            return LevelFamilySubstituteUtil.ResolveAcceptedLevel(GetRequiredInputItemId(recipe, inputIndex), itemId);
        }

        // スロットslotIndexが素材itemIdの束縛先か
        // Whether slot slotIndex is bound to input itemId
        public static bool IsInputBoundTo(MachineRecipeMasterElement recipe, int slotIndex, ItemId itemId)
        {
            if (slotIndex < 0 || recipe.InputItems.Length <= slotIndex) return false;
            return ResolveInputAcceptedLevel(recipe, slotIndex, itemId).HasValue;
        }

        // 入力タンクiが受け入れる液体か
        // Whether input tank i accepts the fluid
        public static bool IsInputFluidBoundTo(MachineRecipeMasterElement recipe, int tankIndex, FluidId fluidId)
        {
            if (tankIndex < 0 || recipe.InputFluids.Length <= tankIndex) return false;
            return MasterHolder.FluidMaster.GetFluidId(recipe.InputFluids[tankIndex].FluidGuid) == fluidId;
        }

        // 出力 outputIndex のスロットへ入れられるアイテム（下位から順。生産物のレベルファミリーの全変種。C9 の DTO も使う）
        // Items the slot of output outputIndex accepts, lowest first: every variant of the product's level family (C9's DTO uses it too)
        public static IReadOnlyList<ItemId> GetAcceptedOutputItemIds(MachineRecipeMasterElement recipe, int outputIndex)
        {
            return MasterHolder.ItemMaster.GetLevelVariants(MasterHolder.ItemMaster.GetItemId(recipe.OutputItems[outputIndex].ItemGuid));
        }

        // 出力スロットごとの既定許可集合（生産物のレベルファミリーのみ）。レシピ未選択は空
        // Default allowed-item set per output slot (level family only); empty when unselected
        public static IReadOnlyList<IReadOnlyCollection<ItemId>> BuildDefaultOutputBinding(MachineRecipeMasterElement recipe)
        {
            if (recipe == null) return System.Array.Empty<IReadOnlyCollection<ItemId>>();

            var binding = new List<IReadOnlyCollection<ItemId>>(recipe.OutputItems.Length);
            for (var j = 0; j < recipe.OutputItems.Length; j++)
            {
                binding.Add(new HashSet<ItemId>(GetAcceptedOutputItemIds(recipe, j)));
            }
            return binding;
        }

        // 入力 inputIndex の要求アイテム（レシピに書いたまま）
        // Required item of input inputIndex, as written in the recipe
        private static ItemId GetRequiredInputItemId(MachineRecipeMasterElement recipe, int inputIndex)
        {
            return MasterHolder.ItemMaster.GetItemId(recipe.InputItems[inputIndex].ItemGuid);
        }
    }
}
