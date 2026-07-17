using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.EnergySystem;

namespace Game.Block.Blocks.Machine
{
    /// <summary>
    ///     機械を表すクラス。所属セグメントの確定済み供給率から実効電力を導出してProcessorへ渡す
    ///     Machine block; derives effective power from its segment's settled supply rate and feeds the processor
    /// </summary>
    public class VanillaElectricMachineComponent : IElectricConsumer, IElectricTickPostHandler
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

        public void OnElectricTickPostProcess(ElectricNetworkStatistics statistics)
        {
            BlockException.CheckDestroy(this);

            // 確定した供給率から実効電力を一度だけProcessorへ渡す
            // Push effective power to the processor once from the settled supply rate
            _vanillaMachineProcessorComponent.SupplyExternalPower(RequestEnergy.AsPrimitive() * statistics.PowerRate);
        }
    }
}
