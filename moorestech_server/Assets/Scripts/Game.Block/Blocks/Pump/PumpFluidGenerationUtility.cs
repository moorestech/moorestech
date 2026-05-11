using System;
using System.Collections.Generic;
using Core.Master;
using Core.Update;
using Game.Context;
using Game.Fluid;
using Mooresmaster.Model.GenerateFluidsModule;
using UnityEngine;

namespace Game.Block.Blocks.Pump
{
    /// <summary>
    /// Shared helper for converting supplied power into fluid generation results.
    /// </summary>
    public static class PumpFluidGenerationUtility
    {
        public static void GenerateFluids(Element[] generateFluids, float powerRate, PumpFluidOutputComponent output, Vector3Int blockPos)
        {
            if (powerRate <= 0f || output == null || generateFluids == null || generateFluids.Length == 0)
            {
                return;
            }

            // 設置位置にFluidMapVeinが無ければ何も生成しない（Minerと同仕様）
            // No generation if no FluidMapVein at this position (same spec as Miner)
            var veins = ServerContext.FluidMapVeinDatastore.GetOverVeins(blockPos);
            if (veins.Count == 0) return;

            var veinFluidIds = new HashSet<FluidId>();
            foreach (var vein in veins) veinFluidIds.Add(vein.VeinFluidId);

            // tick数を秒数に変換
            // Convert ticks to seconds
            var deltaSeconds = GameUpdater.SecondsPerTick;

            foreach (var gen in generateFluids)
            {
                if (gen.GenerateTime <= 0) continue;

                var fluidId = MasterHolder.FluidMaster.GetFluidId(gen.FluidGuid);
                // VeinのFluidIdに無い液体はスキップ（マスタは生成レート表として機能）
                // Skip fluids not present in any vein at this position
                if (!veinFluidIds.Contains(fluidId)) continue;

                var perSecond = gen.Amount / Math.Max(0.0001, gen.GenerateTime);
                var addAmount = perSecond * powerRate * deltaSeconds;
                if (addAmount <= 0) continue;

                var stack = new FluidStack(addAmount, fluidId);
                output.EnqueueGeneratedFluid(stack);
            }
        }
    }
}
