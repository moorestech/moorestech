using Game.Block.Blocks.ElectricWire;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.EnergySystem;

namespace Game.Block.Blocks.Miner
{
    // 所属セグメントの確定済み供給率から実効電力を導出してProcessorへ渡す採掘機
    // Miner deriving effective power from its segment's settled supply rate and feeding the processor
    public class VanillaElectricMinerComponent : IElectricConsumer, IUpdatableBlockComponent
    {
        public BlockInstanceId BlockInstanceId { get; }
        public ElectricPower RequestEnergy => new(_vanillaMinerProcessorComponent.RequestEnergy);

        private readonly VanillaMinerProcessorComponent _vanillaMinerProcessorComponent;

        public VanillaElectricMinerComponent(BlockInstanceId blockInstanceId, VanillaMinerProcessorComponent vanillaMinerProcessorComponent)
        {
            _vanillaMinerProcessorComponent = vanillaMinerProcessorComponent;
            BlockInstanceId = blockInstanceId;
        }

        public void Update()
        {
            BlockException.CheckDestroy(this);

            // 実効電力 = 要求電力 × 所属セグメントの確定済み供給率
            // Effective power = requested power x the segment's settled supply rate
            var powerRate = ElectricSegmentPowerRateResolver.GetPowerRate(BlockInstanceId);
            _vanillaMinerProcessorComponent.SupplyExternalPower(RequestEnergy.AsPrimitive() * powerRate);
        }

        public bool IsDestroy { get; private set; }
        public void Destroy()
        {
            IsDestroy = true;
        }
    }
}
