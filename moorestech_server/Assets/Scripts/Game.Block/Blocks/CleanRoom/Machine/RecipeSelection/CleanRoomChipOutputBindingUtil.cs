using System.Collections.Generic;
using System.Linq;
using Core.Master;
using Game.Block.Blocks.Machine.RecipeSelection;
using Game.Block.Blocks.Machine.State;
using Mooresmaster.Model.MachineRecipesModule;

namespace Game.Block.Blocks.CleanRoom.Machine.RecipeSelection
{
    /// <summary>
    ///     クリーンルーム機械の出力スロット束縛を、生産物ファミリーとチップ差し替え候補まで広げる補助
    ///     Widens a clean-room machine's output slot binding to cover both the product family and chip-swap candidates
    /// </summary>
    internal static class CleanRoomChipOutputBindingUtil
    {
        // 出力スロットの許可集合を「生産物ファミリー∪当該レシピの全ChipItemGuid」へ広げる（2026-08-30裁定D3）。
        // チップ差し替えは完了直前(OnExit)に起きるため、束縛は開始時から広げておく必要がある
        // Widen the output slot's allowed set to "output level family ∪ every chip item guid of the recipe" (2026-08-30 ruling D3).
        // Chip replacement happens right before completion (OnExit), so the binding must already be widened at selection time
        public static void Widen(MachineProcessContext context, MachineRecipeMasterElement recipe)
        {
            if (recipe == null || !MasterHolder.CleanRoomMaster.TryGetChipDraw(recipe.MachineRecipeGuid, out var chipDraw)) return;

            var widened = MachineRecipeSlotBindingUtil.BuildDefaultOutputBinding(recipe)
                .Select(allowed => new HashSet<ItemId>(allowed))
                .ToList();

            foreach (var distribution in chipDraw.OutputDistributions)
            {
                var outputItemId = MasterHolder.ItemMaster.GetItemId(distribution.OutputItemGuid);
                for (var i = 0; i < recipe.OutputItems.Length; i++)
                {
                    if (MasterHolder.ItemMaster.GetItemId(recipe.OutputItems[i].ItemGuid) != outputItemId) continue;
                    foreach (var level in distribution.Levels) widened[i].Add(MasterHolder.ItemMaster.GetItemId(level.ChipItemGuid));
                }
            }

            context.OutputInventory.SetBoundOutputs(widened);
        }
    }
}
