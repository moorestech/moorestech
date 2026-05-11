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
        // 設置位置のFluidMapVeinとマスタgenerateFluidの両方に存在するエントリだけブロック生成時に確定する
        // Resolve entries that exist in both the FluidMapVein at this position and the master generateFluid table, once at block creation
        public static List<FluidGenerationEntry> ResolveGenerationEntries(Element[] generateFluids, Vector3Int blockPos)
        {
            var veins = ServerContext.FluidMapVeinDatastore.GetOverVeins(blockPos);
            if (generateFluids == null || generateFluids.Length == 0 || veins.Count == 0) return new List<FluidGenerationEntry>();

            var veinFluidIds = new HashSet<FluidId>();
            foreach (var vein in veins) veinFluidIds.Add(vein.VeinFluidId);
            
            
            var entries = new List<FluidGenerationEntry>();
            foreach (var gen in generateFluids)
            {
                if (gen.GenerateTime <= 0) continue;
                
                var fluidId = MasterHolder.FluidMaster.GetFluidId(gen.FluidGuid);
                if (!veinFluidIds.Contains(fluidId)) continue;

                var perSecond = gen.Amount / Math.Max(0.0001, gen.GenerateTime);
                entries.Add(new FluidGenerationEntry(fluidId, perSecond));
            }
            
            return entries;
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
