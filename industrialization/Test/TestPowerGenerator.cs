using industrialization.Core.Electric;

namespace industrialization.Test
{
    //デバック用で無限に電力を供給できる
    public class TestPowerGenerator : IPowerGenerator
    {
        private readonly int _power;

        public TestPowerGenerator(int power)
        {
            this._power = power;
        }

        public int OutputPower()
        {
            return _power;
        }
    }
}