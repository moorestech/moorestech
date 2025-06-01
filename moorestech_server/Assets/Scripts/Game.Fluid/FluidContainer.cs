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
        
        public double Amount;
        public FluidId FluidId;
        
        public readonly bool IsEmpty;
        public readonly HashSet<FluidContainer> PreviousSourceFluidContainers = new();
        
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
        
        public FluidStack AddLiquid(FluidStack fluidStack, FluidContainer source)
        {
            // パイプ内の液体IDがセットされていない場合は入ってきた液体のidをセットする
            if (FluidId == FluidMaster.EmptyFluidId)
                FluidId = fluidStack.FluidId;

            if (IsEmpty || fluidStack.FluidId != FluidId)
            {
                return fluidStack;
            }
            
            // Prevent immediate backflow within the same update cycle
            if (source != Empty && PreviousSourceFluidContainers.Contains(source))
            {
                return fluidStack;
            }

            if (Capacity - Amount < fluidStack.Amount)
            {
                var addingAmount = Capacity - Amount;
                Amount += addingAmount;
                // FluidContainer.Emptyは特別扱い（シングルトンなので追加しない）
                if (source != Empty)
                {
                    PreviousSourceFluidContainers.Add(source);
                }
                var guid = MasterHolder.FluidMaster.GetFluidMaster(FluidId).FluidGuid;
                return new FluidStack(fluidStack.Amount - addingAmount, fluidStack.FluidId);
            }

            Amount += fluidStack.Amount;
            // FluidContainer.Emptyは特別扱い（シングルトンなので追加しない）
            if (source != Empty)
            {
                PreviousSourceFluidContainers.Add(source);
            }
            return new FluidStack(0, fluidStack.FluidId);
        }
    }
}