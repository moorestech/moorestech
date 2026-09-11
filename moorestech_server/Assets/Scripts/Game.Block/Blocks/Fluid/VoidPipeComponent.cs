using System.Collections.Generic;
using Game.Block.Interface.Component;
using Game.Fluid;

namespace Game.Block.Blocks.Fluid
{
    /// <summary>
    ///     ボイドパイプ本体。受け入れ面に届いた流体を種別を問わず全量消滅させる終端。
    ///     パイプ網からは機械と同じ境界ポート（FluidBoundaryPort）として扱われ、残量0を返すことで「全量受け入れ」を表現する。
    ///     内容量・可変状態を持たないためセーブもBlockState通知も無い（ADR 0056）。
    ///
    ///     The void pipe body: a terminal sink that destroys every fluid reaching its inflow face regardless of kind.
    ///     The pipe network treats it as a boundary port like a machine; returning a zero remainder expresses "accept everything".
    ///     It holds no amount or mutable state, so there is no save state and no BlockState notification (ADR 0056).
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
