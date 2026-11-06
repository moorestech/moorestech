using Core.Master;

namespace Client.Ui.State.Recipe.Data
{
    /// <summary>
    /// 液体と量の組（Web の MachineRecipe.inputFluids の要素）。Guid→ID の解決は値の作り手が済ませる
    /// A fluid paired with an amount (element of the Web MachineRecipe.inputFluids); the value's builder resolves Guid to id
    /// </summary>
    public readonly struct RecipeFluidAmount
    {
        public readonly FluidId FluidId;
        public readonly double Amount;

        public RecipeFluidAmount(FluidId fluidId, double amount)
        {
            FluidId = fluidId;
            Amount = amount;
        }
    }
}
