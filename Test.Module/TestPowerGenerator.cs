using System;
using Core.Electric;

namespace Test.Module
{
    //デバック用で無限に電力を供給できる
    public class TestPowerGenerator : IPowerGenerator
    {
        public int EntityId { get; }

        private readonly int _power;

        public TestPowerGenerator(int power, int entityId)
        {
            _power = power;
            EntityId = entityId;
        }

        public int OutputEnergy()
        {
            return _power;
        }
    }
}