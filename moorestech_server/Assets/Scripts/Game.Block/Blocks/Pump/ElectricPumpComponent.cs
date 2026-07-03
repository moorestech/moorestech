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
        public ElectricPower RequestEnergy => new(_requestEnergy.AsPrimitive() * (_processor.CanGenerateFluid ? 1f : _idlePowerRate));
        
        private readonly ElectricPumpProcessorComponent _processor;
        private readonly ElectricPower _requestEnergy;
        private readonly float _idlePowerRate;

        public ElectricPumpComponent(BlockInstanceId blockInstanceId, ElectricPower requestEnergy, float idlePowerRate, ElectricPumpProcessorComponent processor)
        {
            BlockInstanceId = blockInstanceId;
            _requestEnergy = requestEnergy;
            _idlePowerRate = idlePowerRate;
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
