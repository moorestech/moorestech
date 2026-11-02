using System.Collections.Generic;
using Core.Item.Interface;
using Core.Master;
using Core.Update;
using Game.Block.Blocks.Machine.Module;
using Game.Block.Interface.MachineUpgrade;
using Game.Context;
using Game.Fluid;
using Mooresmaster.Model.MachineRecipesModule;

namespace Game.Block.Blocks.Machine.State.Util
{
    internal static class MachineOutputFactoryUtil
    {
        // 素の出力1セットを、生産性の当選時は2セット並べる。セット数は差し替え口へ渡すため返す（品質は一般機械の出力を変えない）
        // Lay out one plain output set, or two when productivity rolls; the set count is returned for the rewrite hook (quality never changes general machine output)
        public static List<IItemStack> CreateRealizedOutputs(MachineRecipeMasterElement recipe, MachineModuleEffect effect, int? inheritedLevel, out int setCount)
        {
            setCount = GameRandom.NextDouble() < effect.ExtraOutputChance ? 2 : 1;
            var outputs = new List<IItemStack>(recipe.OutputItems.Length * setCount);
            for (var set = 0; set < setCount; set++) outputs.AddRange(CreatePlainOutputs(recipe, inheritedLevel));
            return outputs;

            #region Internal

            // 出力1セットを書いたアイテムのまま作る。継承レシピは継承先の出力だけを継承レベルの変種にする
            // Build one output set as written; an inheritance recipe swaps only the target output to the inherited level's variant
            static List<IItemStack> CreatePlainOutputs(MachineRecipeMasterElement recipe, int? inheritedLevel)
            {
                var outputs = new List<IItemStack>(recipe.OutputItems.Length);
                for (var i = 0; i < recipe.OutputItems.Length; i++)
                {
                    var itemId = MachineRecipeLevelInheritanceUtil.ResolveOutputItemId(recipe, i, inheritedLevel);
                    outputs.Add(ServerContext.ItemStackFactory.Create(itemId, recipe.OutputItems[i].Count));
                }
                return outputs;
            }

            #endregion
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


    }
}
