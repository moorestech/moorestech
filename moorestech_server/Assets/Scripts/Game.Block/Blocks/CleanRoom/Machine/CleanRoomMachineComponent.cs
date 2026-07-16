using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.EnergySystem;

namespace Game.Block.Blocks.CleanRoom.Machine
{
    // 所属セグメントの確定済み供給率から実効電力を導出してProcessorへ渡すクリーンルーム機械
    // Clean room machine deriving effective power from its segment's settled supply rate and feeding the processor
    public class CleanRoomMachineComponent : ICleanRoomMachine, IElectricConsumer, IElectricTickPostHandler
    {
        private readonly CleanRoomMachineProcessorComponent _processor;

        public CleanRoomMachineComponent(BlockInstanceId blockInstanceId, CleanRoomMachineProcessorComponent processor)
        {
            _processor = processor;
            BlockInstanceId = blockInstanceId;
        }

        public BlockInstanceId BlockInstanceId { get; }
        public bool IsPolluting => _processor.IsPolluting;
        public ElectricPower RequestEnergy => new(_processor.EffectiveRequestPower);

        public void SetCleanRoomEffect(CleanRoomEffect effect)
        {
            _processor.SetCleanRoomEffect(effect);
        }

        public void OnElectricTickPostProcess(ElectricNetworkStatistics statistics)
        {
            BlockException.CheckDestroy(this);

            // 確定した供給率から実効電力を一度だけProcessorへ渡す
            // Push effective power to the processor once from the settled supply rate
            _processor.SupplyExternalPower(RequestEnergy.AsPrimitive() * statistics.PowerRate);
        }

        public bool IsDestroy { get; private set; }
        public void Destroy()
        {
            IsDestroy = true;
        }
    }
}
