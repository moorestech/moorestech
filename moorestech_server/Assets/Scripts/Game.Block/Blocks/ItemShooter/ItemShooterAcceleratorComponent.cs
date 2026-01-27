using Game.Block.Blocks.Gear;
using Game.Block.Component;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Gear.Common;
using Mooresmaster.Model.BlocksModule;

namespace Game.Block.Blocks.ItemShooter
{
    /// <summary>
    /// シューターアクセラレータコンポーネント（tick化により加速機能は廃止）
    /// Item shooter accelerator component (acceleration disabled after tick conversion)
    /// </summary>
    public class ItemShooterAcceleratorComponent : GearEnergyTransformer, IUpdatableBlockComponent
    {
        public ItemShooterAcceleratorComponent(
            ItemShooterComponentService service,
            ItemShooterAcceleratorBlockParam param,
            BlockInstanceId blockInstanceId,
            BlockConnectorComponent<IGearEnergyTransformer> gearConnector)
            : base(new Torque(param.RequireTorque), blockInstanceId, gearConnector)
        {
            // tick化により加速機能は廃止されました
            // Acceleration feature disabled after tick conversion
        }

        public void Update()
        {
            BlockException.CheckDestroy(this);
            // tick化により加速機能は廃止されました
            // Acceleration feature disabled after tick conversion
        }
    }
}
