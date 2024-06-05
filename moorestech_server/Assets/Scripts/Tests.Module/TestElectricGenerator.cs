using Game.Block.Interface;
using Game.EnergySystem;

namespace Tests.Module
{
    //デバック用で無限に電力を供給できる
    public class TestElectricGenerator : IElectricGenerator
    {
        private readonly int _power;
        
        public TestElectricGenerator(int power, EntityID entityId)
        {
            _power = power;
            EntityId = entityId;
        }
        
        public EntityID EntityId { get; }
        
        public int OutputEnergy()
        {
            return _power;
        }
        
        public bool IsDestroy { get; }
        
        public void Destroy()
        {
        }
    }
}