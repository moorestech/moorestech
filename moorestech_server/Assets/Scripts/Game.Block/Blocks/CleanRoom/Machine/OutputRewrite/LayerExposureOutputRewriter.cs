using System.Collections.Generic;
using Core.Item.Interface;
using Core.Master.CleanRoomRoles;
using Game.Block.Blocks.CleanRoom.Machine.Yield;

namespace Game.Block.Blocks.CleanRoom.Machine.OutputRewrite
{
    // EUV: 層の失敗を1回判定し、成功なら入力と同じグレードの次層ウェハ、失敗なら失敗パターンウェハだけを出す
    // EUV: roll the layer once; success yields the next-layer wafer at the input's grade, failure yields only the failed pattern wafer
    internal static class LayerExposureOutputRewriter
    {
        public static List<IItemStack> Rewrite(LayerExposureRecipeRole role, CleanRoomYieldRollContext context)
        {
            // 失敗率＝クラス別の層失敗率×(1−品質合計による低減)
            // Fail rate = class layer fail rate x (1 - quality-sum reduction)
            var failRate = CleanRoomRoleRates.LayerFailRate(role, context.ClassYield, context.ModuleEffect);
            var isFailed = ChipYieldDraw.DrawLayerFailure(failRate, context.Random);

            // 次層ウェハは工場が継承レベルの変種を当て済みなので、写しの成否で片方の個数を0にするだけ
            // The factory already set the layered wafer to the inherited level's variant, so just zero out one side of the copy by the outcome
            var (itemIds, counts) = RecipeOutputSetBuilder.CopySet(context.FactorySet);
            if (isFailed) counts[role.LayeredOutputIndex] = 0;
            else counts[role.FailedOutputIndex] = 0;
            return RecipeOutputSetBuilder.Repeat(itemIds, counts, context.SetCount);
        }
    }
}
