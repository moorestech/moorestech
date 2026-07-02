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

        // モジュール電力効果を反映した要求電力を返す（Vanillaと同じ）
        // Report the module-effect-adjusted requested power (same as Vanilla)
        public ElectricPower RequestEnergy => new(_processor.EffectiveRequestPower);

        public void SupplyEnergy(ElectricPower power)
        {
            BlockException.CheckDestroy(this);
            _processor.SupplyPower(power.AsPrimitive());
        }
    }
}
