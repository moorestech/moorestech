using System;
using System.Collections.Generic;

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
        public Guid? FluidId;
        
        /// <param name="capacity">液体の許容量</param>
        public FluidContainer(double capacity)
        {
            Capacity = capacity;
        }
        
        /// <summary>
        ///     Create empty container.
        /// </summary>
        private FluidContainer()
        {
            FluidId = Guid.Empty;
            Capacity = 0;
            IsEmpty = true;
        }
        
        public void AddLiquid(FluidStack fluidStack, FluidContainer source, out FluidStack? remain)
        {
            // パイプ内の液体IDがセットされていない場合は入ってきた液体のidをセットする
            FluidId ??= fluidStack.FluidId;
            
            if (IsEmpty || fluidStack.FluidId != FluidId)
            {
                remain = fluidStack;
                return;
            }
            
            if (Capacity - Amount < fluidStack.Amount)
            {
                var addingAmount = Capacity - Amount;
                Amount += addingAmount;
                source.PreviousSourceFluidContainers.Add(this);
                remain = new FluidStack(fluidStack.Amount - addingAmount, FluidId.Value);
                return;
            }
            
            Amount += fluidStack.Amount;
            source.PreviousSourceFluidContainers.Add(this);
            remain = null;
        }
    }
}