using UnitGenerator;

namespace Game.Gear.Common
{
    [UnitOf(typeof(float), UnitGenerateOptions.ArithmeticOperator | UnitGenerateOptions.ValueArithmeticOperator | UnitGenerateOptions.Comparable)]
    public partial struct Torque
    {
    }
}