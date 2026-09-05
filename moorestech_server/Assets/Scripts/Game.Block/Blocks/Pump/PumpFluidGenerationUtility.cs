using System;
using System.Collections.Generic;
using Core.Master;
using Core.Update;
using Game.Block.Interface;
using Game.Block.Interface.Vein;
using Game.Context;
using Game.Fluid;
using Mooresmaster.Model.GenerateFluidsModule;

namespace Game.Block.Blocks.Pump
{
    /// <summary>
    /// Shared helper for converting supplied power into fluid generation results.
    /// </summary>
    public static class PumpFluidGenerationUtility
    {
        // 生成時に一度だけ対象流体を確定
        // Resolves the target fluids once, at block creation
        public static List<FluidGenerationEntry> ResolveGenerationEntries(GenerateFluids generateFluids, BlockPositionInfo footprint)
        {
            var entries = new List<FluidGenerationEntry>();
            var pumpableFluidIds = PumpVeinFootprintJudge.ResolvePumpableFluidIds(generateFluids);
            var targetFluidIds = new HashSet<FluidId>();
            foreach (var vein in ServerContext.FluidMapVeinDatastore.Veins)
            {
                if (!PumpVeinFootprintJudge.IsPumpableVein(footprint, pumpableFluidIds, vein.VeinRangeMin, vein.VeinRangeMax, vein.VeinFluidId)) continue;
                targetFluidIds.Add(vein.VeinFluidId);
            }

            // 同一流体は1本にまとめ、公称量はマスタの並び順で決める
            // Each fluid appears once; the nominal rate follows the master ordering
            foreach (var gen in generateFluids.items)
            {
                if (gen.GenerateTime <= 0) continue;

                var fluidId = MasterHolder.FluidMaster.GetFluidId(gen.FluidGuid);
                if (!targetFluidIds.Remove(fluidId)) continue;

                var perSecond = gen.Amount / Math.Max(0.0001, gen.GenerateTime);
                entries.Add(new FluidGenerationEntry(fluidId, perSecond));
            }

            return entries;
        }

        // 生成対象があり出力タンクが受け入れ可能かの共通判定（電気・歯車ポンプで同一式を共有）
        // Shared check for whether generation targets exist and the output tank can accept them (shared by electric and gear pumps)
        public static bool CanGenerateFluid(List<FluidGenerationEntry> entries, PumpFluidOutputComponent output)
        {
            return 0 < entries.Count && output.CanAcceptGeneratedFluid;
        }

        // tick毎の発行はキャッシュ済みエントリをpowerRateで按分するだけ
        // Per-tick emission just scales cached entries by powerRate
        public static void GenerateFluids(List<FluidGenerationEntry> entries, float powerRate, PumpFluidOutputComponent output)
        {
            foreach (var entry in entries)
            {
                var addAmount = entry.PerSecond * powerRate * GameUpdater.SecondsPerTick;
                if (addAmount <= 0) continue;

                output.EnqueueGeneratedFluid(new FluidStack(addAmount, entry.FluidId));
            }
        }
    }

    public readonly struct FluidGenerationEntry
    {
        public readonly FluidId FluidId;
        public readonly double PerSecond;

        public FluidGenerationEntry(FluidId fluidId, double perSecond)
        {
            FluidId = fluidId;
            PerSecond = perSecond;
        }
    }
}
