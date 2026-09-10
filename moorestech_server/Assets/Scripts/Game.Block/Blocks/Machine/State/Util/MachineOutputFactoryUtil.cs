using System;
using System.Collections.Generic;
using Core.Item.Interface;
using Core.Master;
using Game.Block.Blocks.Machine.Module;
using Game.Context;
using Game.Fluid;
using Mooresmaster.Model.MachineRecipesModule;

namespace Game.Block.Blocks.Machine.State.Util
{
    internal static class MachineOutputFactoryUtil
    {
        private static readonly Random Random = new();

        // ベース1セットと当選時の追加1セットを生成。品質レベルは1サイクル1回だけ引き両セットへ適用する
        // Build one base set plus one extra set when the roll succeeds; the quality level is rolled once per cycle and shared by both sets
        public static List<IItemStack> CreateRealizedOutputs(MachineRecipeMasterElement recipe, MachineModuleEffect effect)
        {
            // レベルが混在すると同じ出力スロットへ別変種が積まれ、空スロットでも収まらない組が生まれる
            // Mixed levels stack different variants into one output slot, creating a pair that never fits even when empty
            var level = RollQualityLevel(effect.QualityShift);
            var outputs = CreateLevelAppliedOutputs(recipe, level);
            if (Random.NextDouble() < effect.ExtraOutputChance) outputs.AddRange(CreateLevelAppliedOutputs(recipe, level));
            return outputs;
        }

        // レシピの液体出力1セットを生成
        // Build one set of the recipe's fluid outputs
        public static List<FluidStack> CreateFluidOutputs(MachineRecipeMasterElement recipe)
        {
            var outputs = new List<FluidStack>(recipe.OutputFluids.Length);
            foreach (var outputFluid in recipe.OutputFluids)
            {
                var fluidId = MasterHolder.FluidMaster.GetFluidId(outputFluid.FluidGuid);
                outputs.Add(new FluidStack(outputFluid.Amount, fluidId));
            }
            return outputs;
        }

        // 品質シフトから今サイクルのレベルを引く。シフト無しは1（素の出力）
        // Roll this cycle's level from the quality shift; no shift means level 1 (the plain output)
        private static int RollQualityLevel(float qualityShift)
        {
            if (qualityShift <= 0f) return 1;

            // 整数部=確定、小数部=抽選で+1
            // Integer part guaranteed; the fraction rolls one more
            var guaranteed = (int)Math.Floor(qualityShift);
            var fraction = qualityShift - guaranteed;
            var extra = Random.NextDouble() < fraction ? 1 : 0;
            return 1 + guaranteed + extra;
        }

        // アイテム出力1セットへ指定レベルを適用して生成
        // Build one output set with the given level applied
        private static List<IItemStack> CreateLevelAppliedOutputs(MachineRecipeMasterElement recipe, int level)
        {
            var outputs = new List<IItemStack>(recipe.OutputItems.Length);
            foreach (var outputItem in recipe.OutputItems)
            {
                var stack = ServerContext.ItemStackFactory.Create(outputItem.ItemGuid, outputItem.Count);
                outputs.Add(ApplyLevel(stack, level));
            }

            return outputs;
        }

        // 指定レベルの上位変種へ差し替える
        // Swap to the level's higher-tier variant
        private static IItemStack ApplyLevel(IItemStack output, int level)
        {
            if (level <= 1 || !MasterHolder.ItemMaster.HasLevelFamily(output.Id)) return output;

            var variantId = MasterHolder.ItemMaster.GetLevelVariantItemId(output.Id, level);
            if (variantId == output.Id) return output;
            return ServerContext.ItemStackFactory.Create(variantId, output.Count);
        }
    }
}
