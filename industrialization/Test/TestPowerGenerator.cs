using industrialization.Electric;

namespace industrialization.Test
{
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