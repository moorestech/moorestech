using Game.Block.Interface;
using Game.EnergySystem;

namespace Game.Block.Blocks.Machine
{
    /// <summary>
    ///     機械を表すクラス
    ///     具体的な処理は各コンポーネントに任せて、このクラスはInterfaceの実装だけを行う
    ///     TODO この処理周辺のコンポーネントの分離をいい感じにする
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
        
        public int RequestEnergy => _vanillaMachineProcessorComponent.RequestPower;
        
        public void SupplyEnergy(int power)
        {
            if (IsDestroy) throw BlockException.IsDestroyedException;
            
            _vanillaMachineProcessorComponent.SupplyPower(power);
        }
        
        #endregion
    }
}