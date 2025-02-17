using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Fluid
{
    /// <remarks>
    ///     速度の違うfluidStackはサポート外
    /// </remarks>
    public class FluidContainer
    {
        public readonly float Capacity;
        /// <summary>
        ///     key: stackのfill元
        /// </summary>
        public readonly Dictionary<FluidContainer, FluidStack> FluidStacks = new();
        public Guid FluidId;
        
        /// <param name="capacity">液体の許容量</param>
        /// <param name="fluidId">内部の液体のID</param>
        public FluidContainer(float capacity, Guid fluidId)
        {
            Capacity = capacity;
            FluidId = fluidId;
        }
        
        //TODO: CurrentCapacityをキャッシュする。現時点では実装の簡単のため毎回計算する
        public float TotalAmount => FluidStacks.Sum(kvp => kvp.Value.Amount);
        
        /// <summary>
        ///     可能な限り液体を入れる
        /// </summary>
        /// <param name="fluidStack">使用するfluidStack</param>
        /// <param name="remainFluidStack">残ったfluidStack</param>
        public void Fill(FluidStack fluidStack, out FluidStack? remainFluidStack)
        {
            (var addingStack, FluidStack? remainStack) = Split(fluidStack);
            
            if (!FluidStacks.ContainsKey(addingStack.PreviousFluidContainer))
            {
                FluidStacks[addingStack.PreviousFluidContainer] = new FluidStack(
                    FluidId,
                    0,
                    this,
                    addingStack.FluidContainer
                );
            }
            var currentStack = FluidStacks[addingStack.PreviousFluidContainer];
            currentStack.Amount += addingStack.Amount;
            FluidStacks[addingStack.PreviousFluidContainer] = currentStack;
            
            remainFluidStack = remainStack;
        }
        
        /// <summary>
        ///     FluidStackから可能な限り指定された排出量を排出する
        /// </summary>
        /// <param name="maxDrain">最大排出量</param>
        /// <param name="previousFluidContainer">fill元</param>
        /// <returns>排出したfluidStack</returns>
        public FluidStack Drain(float maxDrain, FluidContainer previousFluidContainer)
        {
            var amount = 0f;
            
            
            var currentStack = FluidStacks[previousFluidContainer];
            
            if (currentStack.Amount < maxDrain - amount)
            {
                // 余らない場合
                amount += currentStack.Amount;
                currentStack.Amount = 0;
            }
            else
            {
                // 余る場合
                var addingAmount = maxDrain - amount;
                amount += addingAmount;
                currentStack.Amount -= addingAmount;
            }
            
            FluidStacks[previousFluidContainer] = currentStack;
            
            return new FluidStack(FluidId, amount, this, previousFluidContainer);
        }
        
        private (FluidStack stack, FluidStack? remain) Split(FluidStack fluidStack)
        {
            // 容量を超えない場合は分けない
            if (!(TotalAmount + fluidStack.Amount > Capacity)) return (fluidStack, null);
            
            var remainAmount = fluidStack.Amount - (Capacity - TotalAmount);
            var remainFluidStack = new FluidStack(fluidStack.FluidId, remainAmount, this, fluidStack.PreviousFluidContainer);
            var newFluidStack = new FluidStack(fluidStack.FluidId, fluidStack.Amount - remainAmount, this, fluidStack.PreviousFluidContainer);
            return (newFluidStack, remainFluidStack);
        }
    }
}