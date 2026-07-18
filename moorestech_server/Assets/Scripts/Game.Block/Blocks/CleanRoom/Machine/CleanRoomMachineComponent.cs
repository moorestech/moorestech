using Game.Block.Blocks.ElectricWire;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.EnergySystem;

namespace Game.Block.Blocks.CleanRoom.Machine
{
    // 所属セグメントの確定済み供給率から実効電力を導出してProcessorへ渡すクリーンルーム機械
    // Clean room machine deriving effective power from its segment's settled supply rate and feeding the processor
    public class CleanRoomMachineComponent : ICleanRoomMachine, IElectricConsumer, IUpdatableBlockComponent
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

        public void Update()
        {
            BlockException.CheckDestroy(this);

            // 実効電力 = 要求電力 × 所属セグメントの確定済み供給率
            // Effective power = requested power x the segment's settled supply rate
            var powerRate = ElectricSegmentPowerRateResolver.GetPowerRate(BlockInstanceId);
            _processor.SupplyExternalPower(RequestEnergy.AsPrimitive() * powerRate);
        }

        public bool IsDestroy { get; private set; }
        public void Destroy()
        {
            IsDestroy = true;
        }
    }
}
