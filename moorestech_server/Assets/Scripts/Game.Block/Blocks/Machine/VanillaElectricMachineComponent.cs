using Game.Block.Interface;
using Game.EnergySystem;

namespace Game.Block.Blocks.Machine
{
    /// <summary>
    ///     機械を表すクラス
    /// </summary>
    public class VanillaElectricMachineComponent : IElectricConsumer
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
        
        #region IBlockElectric implementation
        
        // プロセス中はモジュール効果（省エネ・速度トレードオフ）を反映した要求電力を返す
        // While processing, return the request power adjusted by module effects (efficiency / speed tradeoff)
        public ElectricPower RequestEnergy => new ElectricPower(_vanillaMachineProcessorComponent.EffectiveRequestPower);

        public void SupplyEnergy(ElectricPower power)
        {
            BlockException.CheckDestroy(this);

            _vanillaMachineProcessorComponent.SupplyPower(power.AsPrimitive());
        }
        
        #endregion
    }
}