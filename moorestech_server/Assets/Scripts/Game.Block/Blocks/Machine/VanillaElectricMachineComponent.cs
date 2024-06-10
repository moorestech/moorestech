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
        
        public ElectricPower RequestEnergy => _vanillaMachineProcessorComponent.RequestPower;
        
        public void SupplyEnergy(ElectricPower power)
        {
            BlockException.CheckDestroy(this);
            
            _vanillaMachineProcessorComponent.SupplyPower(power);
        }
        
        #endregion
    }
}