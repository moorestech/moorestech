using Game.Block.Blocks.ElectricWire;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.EnergySystem;

namespace Game.Block.Blocks.Pump
{
    /// <summary>
    /// 所属セグメントの確定済み供給率から実効電力を導出してポンプProcessorへ渡す
    /// Derives effective power from its segment's settled supply rate and feeds the pump processor
    /// </summary>
    public class ElectricPumpComponent : IElectricConsumer, IUpdatableBlockComponent
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

        public void Update()
        {
            BlockException.CheckDestroy(this);

            // 実効電力 = 要求電力 × 所属セグメントの確定済み供給率
            // Effective power = requested power x the segment's settled supply rate
            var powerRate = ElectricSegmentPowerRateResolver.GetPowerRate(BlockInstanceId);
            _processor.SupplyExternalPower(new ElectricPower(RequestEnergy.AsPrimitive() * powerRate));
        }

        public bool IsDestroy { get; private set; }
        public void Destroy()
        {
            IsDestroy = true;
        }
    }
}
