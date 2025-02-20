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
        public static readonly FluidContainer Empty = new();
        public readonly float Capacity;
        /// <summary>
        ///     key: stackのtargetFluidContainer
        /// </summary>
        public readonly Dictionary<FluidContainer, FluidStack> FluidStacks = new();
        
        public readonly bool IsEmpty;
        /// <summary>
        ///     まだ次の移動先が決まっていないfluidStackのリスト
        /// </summary>
        public readonly List<FluidStack> PendingFluidStacks = new();
        public Guid FluidId;
        
        /// <param name="capacity">液体の許容量</param>
        /// <param name="fluidId">内部の液体のID</param>
        public FluidContainer(float capacity, Guid fluidId)
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
        
        //TODO: CurrentCapacityをキャッシュする。現時点では実装の簡単のため毎回計算する
        public float TotalAmount => FluidStacks.Sum(kvp => kvp.Value.Amount);
        
        /// <summary>
        ///     PendingListに追加する
        /// </summary>
        /// <param name="fluidStack">使用するfluidStack</param>
        /// <param name="remainFluidStack">残ったfluidStack</param>
        public void AddToPendingList(FluidStack fluidStack, out FluidStack? remainFluidStack)
        {
            (var addingStack, FluidStack? remainStack) = Split(fluidStack);
            addingStack.TargetContainer = Empty;
            
            PendingFluidStacks.Add(addingStack);
            
            remainFluidStack = remainStack;
        }
        
        /// <summary>
        ///     可能な限り液体を入れる
        /// </summary>
        /// <param name="fluidStack">使用するfluidStack</param>
        /// <param name="targetFluidContainer">入れた液体の次の移動先</param>
        /// <param name="remainFluidStack">残ったfluidStack</param>
        public void Fill(FluidStack fluidStack, FluidContainer targetFluidContainer, out FluidStack? remainFluidStack)
        {
            (var addingStack, FluidStack? remainStack) = Split(fluidStack);
            addingStack.TargetContainer = targetFluidContainer;
            
            if (!FluidStacks.ContainsKey(addingStack.TargetContainer))
            {
                FluidStacks[addingStack.TargetContainer] = new FluidStack(
                    FluidId,
                    0,
                    addingStack.TargetContainer
                );
            }
            var currentStack = FluidStacks[addingStack.TargetContainer];
            currentStack.Amount += addingStack.Amount;
            FluidStacks[addingStack.TargetContainer] = currentStack;
            
            remainFluidStack = remainStack;
        }
        
        /// <summary>
        ///     FluidStackから可能な限り指定された排出量を排出する
        /// </summary>
        /// <param name="maxDrain">最大排出量</param>
        /// <param name="targetFluidContainer">次のターゲットの液体コンテナ</param>
        /// <returns>排出したfluidStack</returns>
        public FluidStack Drain(float maxDrain, FluidContainer targetFluidContainer)
        {
            var amount = 0f;
            
            
            var currentStack = FluidStacks[targetFluidContainer];
            
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
            
            FluidStacks[targetFluidContainer] = currentStack;
            
            return new FluidStack(FluidId, amount, Empty);
        }
        
        private (FluidStack stack, FluidStack? remain) Split(FluidStack fluidStack)
        {
            // 容量を超えない場合は分けない
            if (!(TotalAmount + fluidStack.Amount > Capacity)) return (fluidStack, null);
            
            var remainAmount = fluidStack.Amount - (Capacity - TotalAmount);
            var remainFluidStack = new FluidStack(fluidStack.FluidId, remainAmount, fluidStack.TargetContainer);
            var newFluidStack = new FluidStack(fluidStack.FluidId, fluidStack.Amount - remainAmount, Empty);
            return (newFluidStack, remainFluidStack);
        }
    }
}