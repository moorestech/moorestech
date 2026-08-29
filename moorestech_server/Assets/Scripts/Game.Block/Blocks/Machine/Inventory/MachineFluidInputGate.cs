using Game.Fluid;

namespace Game.Block.Blocks.Machine.Inventory
{
    /// <summary>
    ///     入力液体タンクへの流入を選択レシピの束縛で判定するゲート。指定タンク・未指定流入の両方を扱う（ADR 0042 R5）
    ///     Gates inflow into the input fluid tanks against the selected recipe's binding; handles both designated and undesignated inflow (ADR 0042 R5)
    /// </summary>
    internal static class MachineFluidInputGate
    {
        // 指定タンクは束縛液体の時だけ受け入れ、指定無しは束縛タンクへ直行する
        // A designated tank accepts only its bound fluid; undesignated inflow goes straight to the bound tank
        public static FluidStack Add(VanillaMachineInputInventory inputInventory, FluidStack fluidStack, int designatedTankIndex, out bool changed)
        {
            var index = ResolveTargetIndex();
            if (index < 0)
            {
                changed = false;
                return fluidStack;
            }

            var result = inputInventory.FluidInputSlot[index].AddLiquid(fluidStack);
            changed = 0 < result.AcceptedAmount;
            return result.Remainder;

            #region Internal

            // タンク指定ありは束縛の合否のみ、指定無しは束縛タンクを先頭から探索する
            // A designated tank is judged solely on the binding; undesignated inflow scans for the bound tank from the front
            int ResolveTargetIndex()
            {
                if (0 <= designatedTankIndex && designatedTankIndex < inputInventory.FluidInputSlot.Count)
                {
                    return inputInventory.IsFluidAllowedAt(designatedTankIndex, fluidStack.FluidId) ? designatedTankIndex : -1;
                }

                for (var i = 0; i < inputInventory.FluidInputSlot.Count; i++)
                {
                    if (inputInventory.IsFluidAllowedAt(i, fluidStack.FluidId)) return i;
                }
                return -1;
            }

            #endregion
        }
    }
}
