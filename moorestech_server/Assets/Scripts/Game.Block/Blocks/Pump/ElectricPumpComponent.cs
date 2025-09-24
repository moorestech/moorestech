using Game.Block.Interface;
using Game.EnergySystem;

namespace Game.Block.Blocks.Pump
{
    /// <summary>
    /// Electric consumer that supplies power to the pump processor.
    /// </summary>
    public class ElectricPumpComponent : IElectricConsumer
    {
        public BlockInstanceId BlockInstanceId { get; }
        public ElectricPower RequestEnergy { get; }
        
        private readonly ElectricPumpProcessorComponent _processor;

        public ElectricPumpComponent(BlockInstanceId blockInstanceId, ElectricPower requestEnergy, ElectricPumpProcessorComponent processor)
        {
            BlockInstanceId = blockInstanceId;
            RequestEnergy = requestEnergy;
            _processor = processor;
        }

        
        public void SupplyEnergy(ElectricPower power)
        {
            // 供給された電力をそのまま処理系へ橋渡しする（自前の状態は持たない）
            BlockException.CheckDestroy(this);
            _processor.SupplyPower(power);
        }

        public bool IsDestroy { get; private set; }
        public void Destroy()
        {
            IsDestroy = true;
        }
    }
}
