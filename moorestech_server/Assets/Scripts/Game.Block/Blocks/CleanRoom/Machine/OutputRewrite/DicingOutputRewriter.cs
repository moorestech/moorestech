using System.Collections.Generic;
using Core.Item.Interface;
using Core.Master;
using Core.Master.CleanRoomRoles;
using Game.Block.Blocks.CleanRoom.Machine.Yield;
using Game.Block.Blocks.CleanRoom.Machine.YieldPreview;

namespace Game.Block.Blocks.CleanRoom.Machine.OutputRewrite
{
    // ダイシング: 1枚を diesPerWafer 個に切り、損失を失敗ダイへ、残りを品質の近づき度で混ぜた表でレベル別か失敗ダイへ振り分ける
    // Dicing: cut one wafer into diesPerWafer dies, send losses to failed dies and split survivors by the table blended by the quality approach weight
    internal static class DicingOutputRewriter
    {
        public static List<IItemStack> Rewrite(DicingRecipeRole role, DicingPreview rates, CleanRoomYieldRollContext context)
        {
            // 表示と同じ1回計算の率で引く
            // Draw with the once-computed rates the display records
            var result = ChipYieldDraw.DrawDicing(role.Dicing.DiesPerWafer, rates.DieLossRate, rates.LevelRates, rates.FailedRate, context.Random);

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
