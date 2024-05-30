using Game.Block.Interface.Component;
using Game.EnergySystem;

namespace Game.Block.Blocks.ElectricPole
{
    public class VanillaElectricPoleComponent : IElectricTransformer, IBlockComponent
    {
        public VanillaElectricPoleComponent(int entityId)
        {
            EntityId = entityId;
        }
        
        public int EntityId { get; }
        public bool IsDestroy { get; private set; }
        
        public void Destroy()
        {
            IsDestroy = true;
        }
    }
}