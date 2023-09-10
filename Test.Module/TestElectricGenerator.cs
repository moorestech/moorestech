using System;
using Core.EnergySystem.Electric;

namespace Test.Module
{
    //デバック用で無限に電力を供給できる
    public class TestElectricGenerator : IElectricGenerator
    {
        public int EntityId { get; }

        private readonly int _power;

        public TestElectricGenerator(int power, int entityId)
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