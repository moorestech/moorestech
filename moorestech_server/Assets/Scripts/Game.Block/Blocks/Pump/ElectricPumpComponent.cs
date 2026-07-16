using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.EnergySystem;

namespace Game.Block.Blocks.Pump
{
    /// <summary>
    /// 所属セグメントの確定済み供給率から実効電力を導出してポンプProcessorへ渡す
    /// Derives effective power from its segment's settled supply rate and feeds the pump processor
    /// </summary>
    public class ElectricPumpComponent : IElectricConsumer, IElectricTickPostHandler
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

        public void OnElectricTickPostProcess(ElectricNetworkStatistics statistics)
        {
            BlockException.CheckDestroy(this);

            // 確定した供給率から実効電力を一度だけProcessorへ渡す
            // Push effective power to the processor once from the settled supply rate
            _processor.SupplyExternalPower(new ElectricPower(RequestEnergy.AsPrimitive() * statistics.PowerRate));
        }

        public bool IsDestroy { get; private set; }
        public void Destroy()
        {
            IsDestroy = true;
        }
    }
}
