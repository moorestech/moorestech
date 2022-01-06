using Core.Electric;

namespace Test.Module
{
    //デバック用で無限に電力を供給できる
    public class TestPowerGenerator : IPowerGenerator
    {
        private readonly int _power;
        private int id;

        public TestPowerGenerator(int power, int id)
        {
            this._power = power;
            this.id = id;
        }

        public int OutputPower()
        {
            return _power;
        }

        public int GetIntId()
        {
            return id;
        }
    }
}