using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.EnergySystem;

namespace Game.Block.Blocks.CleanRoom.Machine
{
    public class CleanRoomMachineComponent : ICleanRoomMachine, IElectricConsumer
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

        public void SupplyEnergy(ElectricPower power)
        {
            BlockException.CheckDestroy(this);
            _processor.SupplyPower(power.AsPrimitive());
        }

        public bool IsDestroy { get; private set; }
        public void Destroy()
        {
            IsDestroy = true;
        }
    }
}
