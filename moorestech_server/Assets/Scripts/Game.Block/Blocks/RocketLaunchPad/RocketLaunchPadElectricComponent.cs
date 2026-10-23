using Game.Block.Interface;
using Game.EnergySystem;
using static Game.Block.Interface.BlockException;

namespace Game.Block.Blocks.RocketLaunchPad
{
    // 建造中だけ電力を要求し、供給された量を進行へ渡す
    // Requests power only while building and hands the supplied amount to the processor
    public class RocketLaunchPadElectricComponent : IElectricConsumer, IElectricTickPostHandler
    {
        public BlockInstanceId BlockInstanceId { get; }
        public ElectricPower RequestEnergy => new(_processor.RequestPower);
        public bool IsDestroy { get; private set; }

        private readonly RocketLaunchPadProcessorComponent _processor;

        public RocketLaunchPadElectricComponent(BlockInstanceId blockInstanceId, RocketLaunchPadProcessorComponent processor)
        {
            BlockInstanceId = blockInstanceId;
            _processor = processor;
        }

        public void OnElectricTickPostProcess(ElectricNetworkStatistics statistics)
        {
            CheckDestroy(this);
            _processor.SupplyPower(_processor.RequestPower * statistics.PowerRate);
        }

        public void Destroy()
        {
            IsDestroy = true;
        }
    }
}
