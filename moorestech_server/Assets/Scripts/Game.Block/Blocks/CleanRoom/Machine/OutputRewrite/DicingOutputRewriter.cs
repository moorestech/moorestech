using System;
using System.Collections.Generic;
using Core.Item.Interface;
using Core.Master;
using Core.Master.CleanRoomRoles;
using Game.Block.Blocks.CleanRoom.Machine.Yield;

namespace Game.Block.Blocks.CleanRoom.Machine.OutputRewrite
{
    // ダイシング: 1枚を diesPerWafer 個に切り、損失を失敗ダイへ、残りを品質Tierのダイ表でレベル別か失敗ダイへ振り分ける
    // Dicing: cut one wafer into diesPerWafer dies, send losses to failed dies and split survivors by the quality tier's die table into levels or failed dies
    internal static class DicingOutputRewriter
    {
        public static List<IItemStack> Rewrite(DicingRecipeRole role, CleanRoomYieldRollContext context)
        {
            // 暫定: 最高品質Tierの表をそのまま使う（レア枠シフトは Task 2 で入れる）
            // Interim: use the highest quality tier's table as-is (the rare-slot shift arrives in Task 2)
            var dicing = role.Dicing;
            var tier = Math.Clamp(context.ModuleEffect.HighestQualityModuleTier, 0, dicing.QualityTierDieTables.Length - 1);
            var table = dicing.QualityTierDieTables[tier];
            var rates = new List<float>(table.LevelRates) { table.FailedRate };
            var lossRate = CleanRoomRoleRates.DieLossRate(role, context.ClassYield, context.ModuleEffect);
            var result = ChipYieldDraw.DrawDicing(dicing.DiesPerWafer, lossRate, rates, context.Random);

            // 出力は [ダイLv1..LvN, 失敗ダイ]。表の末尾（失敗枠）は損失と合わせて失敗ダイへ出す
            // Outputs are [die Lv1..LvN, failed die]; the table's last slot (failure) joins the losses as failed dies
            var (itemIds, counts) = RecipeOutputSetBuilder.CopySet(context.FactorySet);
            for (var levelIndex = 0; levelIndex < role.DieLevelCount; levelIndex++)
            {
                itemIds[levelIndex] = MasterHolder.ItemMaster.GetLevelVariantItemId(role.DieBaseItemId, levelIndex + 1);
                counts[levelIndex] = result.GoodDieCountsByLevel[levelIndex];
            }
            counts[role.DieLevelCount] = result.FailedDieCount + result.GoodDieCountsByLevel[role.DieLevelCount];
            return RecipeOutputSetBuilder.Repeat(itemIds, counts, context.SetCount);
        }
    }
}
