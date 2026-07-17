using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.EnergySystem;

namespace Game.Block.Blocks.Miner
{
    // 所属セグメントの確定済み供給率から実効電力を導出してProcessorへ渡す採掘機
    // Miner deriving effective power from its segment's settled supply rate and feeding the processor
    public class VanillaElectricMinerComponent : IElectricConsumer, IElectricTickPostHandler
    {
        public BlockInstanceId BlockInstanceId { get; }
        public ElectricPower RequestEnergy => new(_vanillaMinerProcessorComponent.RequestEnergy);

        private readonly VanillaMinerProcessorComponent _vanillaMinerProcessorComponent;

        public VanillaElectricMinerComponent(BlockInstanceId blockInstanceId, VanillaMinerProcessorComponent vanillaMinerProcessorComponent)
        {
            _vanillaMinerProcessorComponent = vanillaMinerProcessorComponent;
            BlockInstanceId = blockInstanceId;
        }

        public void OnElectricTickPostProcess(ElectricNetworkStatistics statistics)
        {
            BlockException.CheckDestroy(this);

            // 確定した供給率から実効電力を一度だけProcessorへ渡す
            // Push effective power to the processor once from the settled supply rate
            _vanillaMinerProcessorComponent.SupplyExternalPower(RequestEnergy.AsPrimitive() * statistics.PowerRate);
        }

        public bool IsDestroy { get; private set; }
        public void Destroy()
        {
            IsDestroy = true;
        }
    }
}
