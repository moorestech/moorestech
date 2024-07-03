using Game.Block.Interface;
using Game.EnergySystem;

namespace Tests.Module
{
    //デバック用で無限に電力を供給できる
    public class TestElectricGenerator : IElectricGenerator
    {
        private readonly ElectricPower _power;
        
        public TestElectricGenerator(ElectricPower power, BlockInstanceId blockInstanceId)
        {
            _power = power;
            BlockInstanceId = blockInstanceId;
        }
        
        public BlockInstanceId BlockInstanceId { get; }
        
        public ElectricPower OutputEnergy()
        {
            return _power;
        }
        
        public bool IsDestroy { get; }
        
        public void Destroy()
        {
        }
    }
}