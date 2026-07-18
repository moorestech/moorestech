using Game.Block.Blocks.ElectricWire;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.EnergySystem;

namespace Game.Block.Blocks.Machine
{
    /// <summary>
    ///     機械を表すクラス。所属セグメントの確定済み供給率から実効電力を導出してProcessorへ渡す
    ///     Machine block; derives effective power from its segment's settled supply rate and feeds the processor
    /// </summary>
    public class VanillaElectricMachineComponent : IElectricConsumer, IUpdatableBlockComponent
    {
        private readonly VanillaMachineProcessorComponent _vanillaMachineProcessorComponent;

        public VanillaElectricMachineComponent(BlockInstanceId blockInstanceId, VanillaMachineProcessorComponent vanillaMachineProcessorComponent)
        {
            _vanillaMachineProcessorComponent = vanillaMachineProcessorComponent;
            BlockInstanceId = blockInstanceId;
        }
        public BlockInstanceId BlockInstanceId { get; }

        public bool IsDestroy { get; private set; }

        public void Destroy()
        {
            IsDestroy = true;
        }

        // プロセス中はモジュール効果（省エネ・速度トレードオフ）を反映した要求電力を返す
        // While processing, return the request power adjusted by module effects (efficiency / speed tradeoff)
        public ElectricPower RequestEnergy => new(_vanillaMachineProcessorComponent.EffectiveRequestPower);

        public void Update()
        {
            BlockException.CheckDestroy(this);

            // 実効電力 = 要求電力 × 所属セグメントの確定済み供給率
            // Effective power = requested power x the segment's settled supply rate
            var powerRate = ElectricSegmentPowerRateResolver.GetPowerRate(BlockInstanceId);
            _vanillaMachineProcessorComponent.SupplyExternalPower(RequestEnergy.AsPrimitive() * powerRate);
        }
    }
}
