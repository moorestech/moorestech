using System.Collections.Generic;
using Core.Item.Interface;
using Core.Master;
using Core.Master.CleanRoomRoles;
using Game.Block.Blocks.CleanRoom.Machine.Yield;

namespace Game.Block.Blocks.CleanRoom.Machine.OutputRewrite
{
    // ダイシング: 1枚を diesPerWafer 個に切り、損失を失敗ダイへ、残りをグレードのダイ表でレベル別に振り分ける
    // Dicing: cut one wafer into diesPerWafer dies, send losses to failed dies and split survivors by level with the grade's die table
    internal static class DicingOutputRewriter
    {
        public static List<IItemStack> Rewrite(DicingRecipeRole role, int gradeLevel, CleanRoomYieldRollContext context)
        {
            // グレードは入力ウェハから受け継いだレベル。役割の検証が Inherit と「表の数＝グレード数」を保証するので丸めない
            // The grade is the level inherited from the input wafer; validation guarantees Inherit and one table per grade, so no clamping
            var dicing = role.Dicing;
            var lossRate = CleanRoomRoleRates.DieLossRate(role, context.ClassYield, context.ModuleEffect);
            var result = ChipYieldDraw.DrawDicing(dicing.DiesPerWafer, lossRate, dicing.GradeDieTables[gradeLevel - 1].LevelRates, context.Random);

            // 出力は [ダイLv1..LvN, 失敗ダイ]。工場が継承先（Lv1 の枠）を投入グレードの変種にしているので、レベル列のIDは全部書き直す
            // Outputs are [die Lv1..LvN, failed die]; the factory set the inheritance target (the Lv1 slot) to the input grade's variant, so rewrite every level id
            var (itemIds, counts) = RecipeOutputSetBuilder.CopySet(context.FactorySet);
            for (var levelIndex = 0; levelIndex < role.DieLevelCount; levelIndex++)
            {
                itemIds[levelIndex] = MasterHolder.ItemMaster.GetLevelVariantItemId(role.DieBaseItemId, levelIndex + 1);
                counts[levelIndex] = levelIndex < result.GoodDieCountsByLevel.Count ? result.GoodDieCountsByLevel[levelIndex] : 0;
            }
            counts[role.DieLevelCount] = result.FailedDieCount;
            return RecipeOutputSetBuilder.Repeat(itemIds, counts, context.SetCount);
        }
    }
}
