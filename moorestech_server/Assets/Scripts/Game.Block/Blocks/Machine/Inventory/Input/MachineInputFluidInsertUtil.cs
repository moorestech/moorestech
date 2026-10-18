using System;
using Game.Fluid;

namespace Game.Block.Blocks.Machine.Inventory.Input
{
    // 束縛タンクへの液体挿入を分離する
    // Isolate fluid insertion into recipe-bound tanks
    internal static class MachineInputFluidInsertUtil
    {
        public static FluidStack InsertFluid(FluidContainer[] tanks, MachineInputSlotBinding slotBinding, FluidStack fluidStack, int designatedTankIndex, out bool changed)
        {
            // レシピ未選択の機械はどのタンクも受け入れない
            // An unselected machine accepts nothing in any tank
            if (!slotBinding.IsBound())
            {
                changed = false;
                return fluidStack;
            }

            if (0 <= designatedTankIndex && designatedTankIndex < tanks.Length)
            {
                return InsertIntoDesignatedTank(designatedTankIndex, out changed);
            }

            // 先頭タンクへ全量入れると、同一液体を複数タンクが要求するレシピで後続タンクが永久に空のままになる
            // Dumping everything into the first tank leaves later tanks empty forever when several tanks require the same fluid
            var remaining = fluidStack;
            remaining = FillBoundTanks(remaining, true);
            remaining = FillBoundTanks(remaining, false);

            changed = remaining.Amount < fluidStack.Amount;
            return remaining;

            #region Internal

            FluidStack InsertIntoDesignatedTank(int designated, out bool designatedChanged)
            {
                if (!slotBinding.IsFluidAllowedAt(designated, fluidStack.FluidId))
                {
                    designatedChanged = false;
                    return fluidStack;
                }

                var result = tanks[designated].AddLiquid(fluidStack);
                designatedChanged = 0 < result.AcceptedAmount;
                return result.Remainder;
            }

            // 制限時は必要量、非制限時は容量まで充填
            // Fill to the requirement when limited; otherwise fill to capacity.
            FluidStack FillBoundTanks(FluidStack incoming, bool limitToRequirement)
            {
                foreach (var tankIndex in slotBinding.ResolveBoundTanks(incoming.FluidId))
                {
                    if (incoming.Amount <= 0) break;

                    var container = tanks[tankIndex];
                    var acceptable = limitToRequirement
                        ? Math.Min(incoming.Amount, slotBinding.RequiredFluidAmountAt(tankIndex) - container.Amount)
                        : incoming.Amount;
                    if (acceptable <= 0) continue;

                    var result = container.AddLiquid(new FluidStack(acceptable, incoming.FluidId));
                    incoming = new FluidStack(incoming.Amount - result.AcceptedAmount, incoming.FluidId);
                }
                return incoming;
            }

            #endregion
        }
    }
}
