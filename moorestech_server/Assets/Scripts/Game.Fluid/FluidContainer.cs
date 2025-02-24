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
        public double Amount;
        public Guid FluidId;
        public HashSet<FluidContainer> PreviousSourceFluidContainers = new();
        
        /// <param name="capacity">液体の許容量</param>
        /// <param name="fluidId">内部の液体のID</param>
        public FluidContainer(double capacity, Guid fluidId)
        {
            Capacity = capacity;
            FluidId = fluidId;
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
        
        public void AddLiquid(double amount, FluidContainer previous, out double? remain)
        {
            remain = null;
            if (IsEmpty) return;
            
            if (Capacity - Amount < amount)
            {
                var addingAmount = Capacity - Amount;
                Amount += addingAmount;
                previous.PreviousSourceFluidContainers.Add(this);
                remain = amount - addingAmount;
                return;
            }
            
            Amount += amount;
            previous.PreviousSourceFluidContainers.Add(this);
        }
    }
}