using System;
using Core.Master;
using Core.Update;
using Game.Fluid;
using Mooresmaster.Model.GenerateFluidsModule;

namespace Game.Block.Blocks.Pump
{
    /// <summary>
    /// Shared helper for converting supplied power into fluid generation results.
    /// </summary>
    public static class PumpFluidGenerationUtility
    {
        public static void GenerateFluids(Element[] generateFluids, float powerRate, PumpFluidOutputComponent output)
        {
            if (powerRate <= 0f || output == null || generateFluids == null || generateFluids.Length == 0)
            {
                return;
            }

            var deltaTime = GameUpdater.UpdateSecondTime;

            foreach (var gen in generateFluids)
            {
                if (gen.GenerateTime <= 0) continue;

                var perSecond = gen.Amount / Math.Max(0.0001, gen.GenerateTime);
                var addAmount = perSecond * powerRate * deltaTime;
                if (addAmount <= 0) continue;

                var fluidId = MasterHolder.FluidMaster.GetFluidId(gen.FluidGuid);
                var stack = new FluidStack(addAmount, fluidId);
                output.EnqueueGeneratedFluid(stack);
            }
        }
    }
}
