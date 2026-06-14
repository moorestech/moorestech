using Game.Block.Interface;
using Game.EnergySystem;

namespace Game.Block.Blocks.CleanRoom
{
    // VanillaElectricMachineComponent をコピーし、参照先を専用プロセッサに差し替えた薄い IElectricConsumer。
    // Copied from VanillaElectricMachineComponent; a thin IElectricConsumer pointing at the dedicated processor.
    public class CleanRoomMachineElectricComponent : IElectricConsumer
    {
        private readonly CleanRoomMachineProcessorComponent _processor;

        public CleanRoomMachineElectricComponent(BlockInstanceId blockInstanceId, CleanRoomMachineProcessorComponent processor)
        {
            _processor = processor;
            BlockInstanceId = blockInstanceId;
        }

        public BlockInstanceId BlockInstanceId { get; }

        public bool IsDestroy { get; private set; }

        public void Destroy()
        {
            IsDestroy = true;
        }

        public ElectricPower RequestEnergy => new(_processor.RequestPower);

        public void SupplyEnergy(ElectricPower power)
        {
            BlockException.CheckDestroy(this);
            _processor.SupplyPower(power.AsPrimitive());
        }
    }
}
