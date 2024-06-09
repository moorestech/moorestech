using UnitGenerator;

namespace Game.EnergySystem
{
    [UnitOf(typeof(float),UnitGenerateOptions.ArithmeticOperator | UnitGenerateOptions.ValueArithmeticOperator | UnitGenerateOptions.Comparable | UnitGenerateOptions.ImplicitOperator)]
    public readonly partial struct ElectricPower { }
}