#if NET6_0
using Core.EnergySystem.Electric;

namespace Test.Module
{
    
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
#endif