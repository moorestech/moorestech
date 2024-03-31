using Core.EnergySystem.Electric;

namespace Tests.Module
{
    //デバック用で無限に電力を供給できる
    public class TestElectricGenerator : IElectricGenerator
    {
        private readonly int _power;

        public TestElectricGenerator(int power, int entityId)
        {
            _power = power;
            EntityId = entityId;
        }

        public int EntityId { get; }

        public int OutputEnergy()
        {
            return _power;
        }
    }
}