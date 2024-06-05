using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.EnergySystem;

namespace Game.Block.Blocks.ElectricPole
{
    public class VanillaElectricPoleComponent : IElectricTransformer, IBlockComponent
    {
        public VanillaElectricPoleComponent(EntityID entityId)
        {
            EntityId = entityId;
        }
        
        public EntityID EntityId { get; }
        public bool IsDestroy { get; private set; }
        
        public void Destroy()
        {
            IsDestroy = true;
        }
    }
}