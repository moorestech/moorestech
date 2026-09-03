using System.Collections.Generic;
using System.Linq;
using Core.Master;
using Game.Block.Blocks.Machine.RecipeSelection;
using Mooresmaster.Model.MachineRecipesModule;

namespace Game.Block.Blocks.CleanRoom.Machine.RecipeSelection
{
    /// <summary>
    ///     クリーンルーム機械の出力スロット許可集合を、生産物ファミリーとチップ差し替え候補まで広げて組み立てる純関数
    ///     Pure builder of a clean-room machine's allowed output set, widened to cover both the product family and chip-swap candidates
    /// </summary>
    internal static class CleanRoomChipOutputBindingUtil
    {
        // 出力スロットの許可集合を「生産物ファミリー∪当該レシピの全ChipItemGuid」で組み立てる（2026-08-30裁定D3）。
        // チップ差し替えは実現出力の確定時に起きるため、束縛は選択時点で広げておく必要がある
        // Build the allowed output set as "output level family ∪ every chip item guid of the recipe" (2026-08-30 ruling D3).
        // The chip swap happens when outputs are realized, so the binding must already be widened at selection time
        public static IReadOnlyList<IReadOnlyCollection<ItemId>> BuildOutputBinding(MachineRecipeMasterElement recipe)
        {
            var binding = MachineRecipeSlotBindingUtil.BuildDefaultOutputBinding(recipe);
            if (recipe == null || !MasterHolder.CleanRoomMaster.TryGetChipDraw(recipe.MachineRecipeGuid, out var chipDraw)) return binding;

            var widened = binding.Select(allowed => new HashSet<ItemId>(allowed)).ToList();
            foreach (var distribution in chipDraw.OutputDistributions)
            {
                var outputItemId = MasterHolder.ItemMaster.GetItemId(distribution.OutputItemGuid);
                for (var i = 0; i < recipe.OutputItems.Length; i++)
                {
                    if (MasterHolder.ItemMaster.GetItemId(recipe.OutputItems[i].ItemGuid) != outputItemId) continue;
                    foreach (var level in distribution.Levels) widened[i].Add(MasterHolder.ItemMaster.GetItemId(level.ChipItemGuid));
                }
            }

            return widened;
        }
    }
}
