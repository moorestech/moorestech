using System.Collections.Generic;
using Game.Block.Interface.Component;
using Game.Fluid;

namespace Game.Block.Blocks.Fluid
{
    /// <summary>
    ///     受け入れ面の流体を全量消滅させる終端（ADR 0056）
    ///     A terminal sink that destroys every fluid on its inflow face (ADR 0056)
    /// </summary>
    public class VoidPipeComponent : IFluidInventory
    {
        public FluidStack AddLiquid(FluidStack fluidStack, ConnectedInfo connectedInfo)
        {
            // 全量消滅させ、残量0を同じ流体IDで返す
            // Destroy the full amount and return a zero remainder with the same fluid id
            return new FluidStack(0, fluidStack.FluidId);
        }

        public List<FluidStack> GetFluidInventory()
        {
            return new List<FluidStack>();
        }

        public bool IsDestroy { get; private set; }

        public void Destroy()
        {
            IsDestroy = true;
        }
    }
}
