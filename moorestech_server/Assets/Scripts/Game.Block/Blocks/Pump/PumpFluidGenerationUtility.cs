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

            // 内部タンクは単一流体しか持てないため、マスタの並び順で最初に重なった1流体だけを対象にする
            // The inner tank holds a single fluid, so only the first overlapping fluid in master order becomes the target
            // 同一流体の重複行はBlockMasterUtilの検証で禁止済みなので、ここへは一意な流体しか来ない
            // Duplicate rows for one fluid are rejected by BlockMasterUtil validation, so only unique fluids reach here
            foreach (var gen in generateFluids.items)
            {
                if (gen.GenerateTime <= 0) continue;

                var fluidId = MasterHolder.FluidMaster.GetFluidId(gen.FluidGuid);
                if (!targetFluidIds.Remove(fluidId)) continue;

                var perSecond = gen.Amount / Math.Max(0.0001, gen.GenerateTime);
                entries.Add(new FluidGenerationEntry(fluidId, perSecond));
                break;
            }

            return entries;
        }

        // 稼働率の上限 =（空き容量＋直前tickの搬出量）÷ 満額1tick生成量。満杯でも搬出分だけは稼働し、超過生成に電力を払わない
        // Demand cap = (free space + last tick's push) / full per-tick generation; a full but draining tank runs only as much as it drains, paying nothing for overflow
        public static float GenerationDemandRate(List<FluidGenerationEntry> entries, PumpFluidOutputComponent output)
        {
            var fullGenerationPerTick = 0.0;
            foreach (var entry in entries) fullGenerationPerTick += entry.PerSecond * GameUpdater.SecondsPerTick;
            if (fullGenerationPerTick <= 0) return 0f;

            var acceptableAmount = output.RoomAmount + output.PushedAmountLastUpdate;
            return (float)Math.Clamp(acceptableAmount / fullGenerationPerTick, 0.0, 1.0);
        }

        // 電気・歯車ポンプで共有する稼働判定。上限が0（汲み上げ対象なし・満杯かつ搬出なし）なら待機
        // Generation check shared by electric and gear pumps; a zero cap (no target, or full with no push) means idle
        public static bool CanGenerateFluid(List<FluidGenerationEntry> entries, PumpFluidOutputComponent output)
        {
            return 0f < GenerationDemandRate(entries, output);
        }

        // tick毎の発行はキャッシュ済みエントリをpowerRateで按分するだけ。powerRateは呼び出し側で稼働率の上限を適用済み
        // Per-tick emission just scales cached entries by powerRate; callers have already applied the demand cap to it
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
