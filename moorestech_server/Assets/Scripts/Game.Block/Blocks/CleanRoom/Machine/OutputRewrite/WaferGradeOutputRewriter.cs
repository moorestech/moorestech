using System.Collections.Generic;
using Core.Item.Interface;
using Core.Master;
using Core.Master.CleanRoomRoles;
using Game.Block.Blocks.CleanRoom.Machine.Yield;

namespace Game.Block.Blocks.CleanRoom.Machine.OutputRewrite
{
    // 成膜: グレード付きウェハの出力だけを抽選したグレードの変種へ差し替える
    // Deposition: swap only the graded-wafer output for the variant of the drawn grade
    internal static class WaferGradeOutputRewriter
    {
        public static List<IItemStack> Rewrite(WaferGradeRecipeRole role, CleanRoomYieldRollContext context)
        {
            // 上限＝最高tier+1、昇格確率＝品質合計×クラス倍率（上限maxPromotionChance）で1回引く
            // Draw once with cap = top tier + 1 and chance = quality sum x class multiplier (capped by maxPromotionChance)
            var maxGrade = CleanRoomRoleRates.MaxWaferGrade(role, context.ModuleEffect);
            var chance = CleanRoomRoleRates.PromotionChance(role, context.ClassYield, context.ModuleEffect);
            var grade = ChipYieldDraw.DrawWaferGrade(chance, maxGrade, context.Random);

            // 同じグレードを全セットへ並べる（別グレードが同じスロットで混ざらない）
            // Lay the same grade into every set so different grades never meet in one slot
            var (itemIds, counts) = RecipeOutputSetBuilder.CopySet(context.FactorySet);
            itemIds[role.GradedOutputIndex] = MasterHolder.ItemMaster.GetLevelVariantItemId(role.GradedWaferBaseItemId, grade);
            return RecipeOutputSetBuilder.Repeat(itemIds, counts, context.SetCount);
        }
    }
}
