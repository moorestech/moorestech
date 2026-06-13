using System;
using System.Collections.Generic;
using Core.Item.Interface;
using Core.Master;
using Game.Block.Blocks.Machine.Inventory;
using Game.Block.Blocks.Machine.Module;
using Game.Context;
using Game.Fluid;
using Mooresmaster.Model.MachineRecipesModule;

namespace Game.Block.Blocks.Machine.State
{
    // 加工ステート間で共有する状態と産出物生成ヘルパーをまとめたコンテキスト
    // Context bundling the state shared across processing states and the output helpers
    internal class MachineProcessContext
    {
        // 同tick生成機械の同シード回避のため共有
        // Shared to avoid same-tick identical seeds
        private static readonly Random Random = new();

        public readonly VanillaMachineInputInventory InputInventory;
        public readonly VanillaMachineOutputInventory OutputInventory;
        public readonly MachineModuleEffectComponent EffectComponent;
        public readonly float RequestPower;

        public ProcessState CurrentState = ProcessState.Idle;
        public uint RemainingTicks;
        public MachineRecipeMasterElement ProcessingRecipe;
        // 開始時に確定した産出予定。セーブで引き継ぐ
        // Outputs fixed at start; carried through saves
        public List<IItemStack> PendingOutputs;
        public uint ProcessingRecipeTicks;
        public float CurrentPower;
        public bool UsedPower;

        public MachineProcessContext(
            VanillaMachineInputInventory inputInventory,
            VanillaMachineOutputInventory outputInventory,
            MachineModuleEffectComponent effectComponent,
            float requestPower)
        {
            InputInventory = inputInventory;
            OutputInventory = outputInventory;
            EffectComponent = effectComponent;
            RequestPower = requestPower;
        }

        public Guid RecipeGuid => ProcessingRecipe?.MachineRecipeGuid ?? Guid.Empty;

        public float EffectiveRequestPower => RequestPower *
                                              (CurrentState == ProcessState.Processing ? EffectComponent.AggregateCurrent().PowerMultiplier : 1f);

        // ベース1セットと当選時の追加1セットを生成
        // Build one base set plus one extra set when the roll succeeds
        public List<IItemStack> CreateRealizedOutputs(MachineRecipeMasterElement recipe, MachineModuleEffect effect)
        {
            var outputs = CreateQualityAppliedOutputs(recipe, effect.QualityShift);
            if (Random.NextDouble() < effect.ExtraOutputChance) outputs.AddRange(CreateQualityAppliedOutputs(recipe, effect.QualityShift));
            return outputs;
        }

        // レシピの液体出力1セットを生成
        // Build one set of the recipe's fluid outputs
        public List<FluidStack> CreateFluidOutputs(MachineRecipeMasterElement recipe)
        {
            var outputs = new List<FluidStack>(recipe.OutputFluids.Length);
            foreach (var outputFluid in recipe.OutputFluids)
            {
                var fluidId = MasterHolder.FluidMaster.GetFluidId(outputFluid.FluidGuid);
                outputs.Add(new FluidStack(outputFluid.Amount, fluidId));
            }
            return outputs;
        }

        // アイテム出力1セットに品質抽選を適用して生成
        // Build one output set with quality rolls applied
        private List<IItemStack> CreateQualityAppliedOutputs(MachineRecipeMasterElement recipe, float qualityShift)
        {
            var outputs = new List<IItemStack>(recipe.OutputItems.Length);
            foreach (var outputItem in recipe.OutputItems)
            {
                var stack = ServerContext.ItemStackFactory.Create(outputItem.ItemGuid, outputItem.Count);
                outputs.Add(ApplyQualityLevel(stack, qualityShift));
            }
            return outputs;
        }

        // 品質シフトで上位レベル変種へ差し替える
        // Swap to a higher-level variant per the quality shift
        private IItemStack ApplyQualityLevel(IItemStack output, float qualityShift)
        {
            if (qualityShift <= 0f || !MasterHolder.ItemMaster.HasLevelFamily(output.Id)) return output;

            // 整数部=確定、小数部=抽選で+1
            // Integer part guaranteed; the fraction rolls one more
            var guaranteed = (int)Math.Floor(qualityShift);
            var fraction = qualityShift - guaranteed;
            var extra = Random.NextDouble() < fraction ? 1 : 0;
            var level = 1 + guaranteed + extra;

            var variantId = MasterHolder.ItemMaster.GetLevelVariantItemId(output.Id, level);
            if (variantId == output.Id) return output;
            return ServerContext.ItemStackFactory.Create(variantId, output.Count);
        }
    }
}
