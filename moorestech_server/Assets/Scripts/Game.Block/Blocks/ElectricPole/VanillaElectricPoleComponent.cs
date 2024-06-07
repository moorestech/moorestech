using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.EnergySystem;

namespace Game.Block.Blocks.ElectricPole
{
    public class VanillaElectricPoleComponent : IElectricTransformer, IBlockComponent
    {
        public VanillaElectricPoleComponent(BlockInstanceId blockInstanceId)
        {
            BlockInstanceId = blockInstanceId;
        }
        
        public BlockInstanceId BlockInstanceId { get; }
        public bool IsDestroy { get; private set; }
        
        public void Destroy()
        {
            IsDestroy = true;
        }
    }
}