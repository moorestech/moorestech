using Game.Block.Interface;
using Game.EnergySystem;

namespace Tests.Module
{
    //デバック用で無限に電力を供給できる
    public class TestElectricGenerator : IElectricGenerator
    {
        private ElectricPower _power;

        public TestElectricGenerator(ElectricPower power, BlockInstanceId blockInstanceId)
        {
            _power = power;
            BlockInstanceId = blockInstanceId;
        }

        // テストシナリオ途中で発電量を切り替える（停電の再現等）
        // Switch the generated power mid-scenario (e.g. to simulate an outage)
        public void SetPower(ElectricPower power)
        {
            _power = power;
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