using System.Collections.Generic;
using Core.Item.Interface;
using Core.Master;
using Core.Master.CleanRoomRoles;
using Game.Block.Blocks.CleanRoom.Machine.Yield;

namespace Game.Block.Blocks.CleanRoom.Machine.OutputRewrite
{
    // ダイシング: 1枚を diesPerWafer 個に切り、損失を失敗ダイへ、残りを品質Tierの表（レア枠シフト後）でレベル別か失敗ダイへ振り分ける
    // Dicing: cut one wafer into diesPerWafer dies, send losses to failed dies and split survivors by the quality tier's shifted table into levels or failed dies
    internal static class DicingOutputRewriter
    {
        public static List<IItemStack> Rewrite(DicingRecipeRole role, CleanRoomYieldRollContext context)
        {
            // 表示と同じ口で率を求めて引く
            // Draw with the rates from the same entry point the display uses
            var lossRate = CleanRoomRoleRates.DieLossRate(role, context.ClassYield, context.ModuleEffect);
            var survivorRates = CleanRoomRoleRates.DicingRates(role, context.ClassYield, context.ModuleEffect, out _);
            var result = ChipYieldDraw.DrawDicing(role.Dicing.DiesPerWafer, lossRate, survivorRates, context.Random);

            // 出力は [ダイLv1..LvN, 失敗ダイ]。損失と失敗枠の分は合算済みの失敗ダイへ出す
            // Outputs are [die Lv1..LvN, failed die]; losses and failure-slot picks are already summed into failed dies
            var (itemIds, counts) = RecipeOutputSetBuilder.CopySet(context.FactorySet);
            for (var levelIndex = 0; levelIndex < role.DieLevelCount; levelIndex++)
            {
                itemIds[levelIndex] = MasterHolder.ItemMaster.GetLevelVariantItemId(role.DieBaseItemId, levelIndex + 1);
                counts[levelIndex] = result.GoodDieCountsByLevel[levelIndex];
            }
            counts[role.DieLevelCount] = result.FailedDieCount;
            return RecipeOutputSetBuilder.Repeat(itemIds, counts, context.SetCount);
        }
    }
}
