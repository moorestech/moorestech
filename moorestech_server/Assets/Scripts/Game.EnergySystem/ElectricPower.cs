using UnitGenerator;

namespace Game.EnergySystem
{
    [UnitOf(typeof(float), UnitGenerateOptions.ArithmeticOperator | UnitGenerateOptions.ValueArithmeticOperator | UnitGenerateOptions.Comparable)]
    public readonly partial struct ElectricPower
    {
    }
}