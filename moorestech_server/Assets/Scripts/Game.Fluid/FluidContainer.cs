using System;
using System.Collections.Generic;
using Core.Master;

namespace Game.Fluid
{
    /// <remarks>
    ///     速度の違うfluidStackはサポート外
    /// </remarks>
    public class FluidContainer
    {
        public static readonly FluidContainer Empty = new();
        public readonly double Capacity;
        
        public readonly bool IsEmpty;
        public readonly HashSet<FluidContainer> PreviousSourceFluidContainers = new();
        public double Amount;
        public FluidId FluidId;
        
        /// <param name="capacity">液体の許容量</param>
        public FluidContainer(double capacity)
        {
            Capacity = capacity;
            FluidId = FluidMaster.EmptyFluidId;
        }
        
        /// <summary>
        ///     Create empty container.
        /// </summary>
        private FluidContainer()
        {
            FluidId = FluidMaster.EmptyFluidId;
            Capacity = 0;
            IsEmpty = true;
        }
        
        public void AddLiquid(FluidStack fluidStack, FluidContainer source, out FluidStack? remain)
        {
            // パイプ内の液体IDがセットされていない場合は入ってきた液体のidをセットする
            var stackFluidId = MasterHolder.FluidMaster.GetFluidId(fluidStack.FluidId);
            if (FluidId == FluidMaster.EmptyFluidId)
                FluidId = stackFluidId;

            if (IsEmpty || stackFluidId != FluidId)
            {
                remain = fluidStack;
                return;
            }

            if (Capacity - Amount < fluidStack.Amount)
            {
                var addingAmount = Capacity - Amount;
                Amount += addingAmount;
                source.PreviousSourceFluidContainers.Add(this);
                var guid = MasterHolder.FluidMaster.GetFluidMaster(FluidId).FluidGuid;
                remain = new FluidStack(fluidStack.Amount - addingAmount, guid);
                return;
            }

            Amount += fluidStack.Amount;
            source.PreviousSourceFluidContainers.Add(this);
            remain = null;
        }
    }
}