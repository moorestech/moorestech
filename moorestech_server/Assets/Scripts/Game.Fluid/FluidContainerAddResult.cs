namespace Game.Fluid
{
    /// <summary>
    ///     FluidContainer.AddLiquidの結果。流体の互換判定はAddLiquid内で一元的に行い、呼び出し側はこの結果だけで分岐する。
    ///     Result of FluidContainer.AddLiquid. Fluid compatibility is judged once inside AddLiquid; callers branch on this result alone.
    /// </summary>
    public readonly struct FluidContainerAddResult
    {
        // 受け入れられなかった残り
        // The unaccepted remainder
        public readonly FluidStack Remainder;

        // 実際に受け入れた量
        // The amount actually accepted
        public readonly double AcceptedAmount;

        // 正量の適合流体が届いたか（満タンで受入0でもtrue、異種流体・空スタックはfalse）
        // Whether a positive amount of a compatible fluid arrived (true even when full accepts zero; false for mismatched fluids or empty stacks)
        public readonly bool IsCompatibleSupply;

        public FluidContainerAddResult(FluidStack remainder, double acceptedAmount, bool isCompatibleSupply)
        {
            Remainder = remainder;
            AcceptedAmount = acceptedAmount;
            IsCompatibleSupply = isCompatibleSupply;
        }
    }
}
