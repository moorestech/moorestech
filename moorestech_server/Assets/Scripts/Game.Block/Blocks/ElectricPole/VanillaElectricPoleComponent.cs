using Core.EnergySystem;
using Game.Block.Interface.Component;
using Game.EnergySystem;

namespace Game.Block.Blocks.ElectricPole
{
    public class VanillaElectricPoleComponent : IElectricTransformer, IBlockComponent
    {
        public int EntityId { get; }
        public bool IsDestroy { get; private set; }

        public VanillaElectricPoleComponent(int entityId)
        {
            EntityId = entityId;
        }

        public void Destroy()
        {
            IsDestroy = true;
        }
    }
}