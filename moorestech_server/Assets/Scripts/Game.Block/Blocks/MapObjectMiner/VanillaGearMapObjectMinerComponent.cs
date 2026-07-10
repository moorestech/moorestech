using Game.Block.Blocks.Gear;
using Game.Block.Interface;
using Game.Block.Interface.Component;

namespace Game.Block.Blocks.MapObjectMiner
{
    public class VanillaGearMapObjectMinerComponent : IUpdatableBlockComponent
    {
        private readonly GearEnergyTransformer _gearEnergyTransformer;
        private readonly VanillaGearMapObjectMinerProcessorComponent _vanillaGearMapObjectMinerProcessorComponent;

        public VanillaGearMapObjectMinerComponent(GearEnergyTransformer gearEnergyTransformer, VanillaGearMapObjectMinerProcessorComponent vanillaGearMapObjectMinerProcessorComponent)
        {
            _vanillaGearMapObjectMinerProcessorComponent = vanillaGearMapObjectMinerProcessorComponent;
            _gearEnergyTransformer = gearEnergyTransformer;
        }

        // GearRuntimeStateStore由来の現在供給値を毎tick取り直し、採掘処理より前にprocessorへ渡す
        // Re-read the current supply from GearRuntimeStateStore each tick and feed the processor before it mines
        public void Update()
        {
            BlockException.CheckDestroy(this);
            _vanillaGearMapObjectMinerProcessorComponent.SupplyPower(_gearEnergyTransformer.GetCurrentSuppliedPower().AsPrimitive());
        }

        public bool IsDestroy { get; private set; }
        public void Destroy()
        {
            IsDestroy = true;
        }
    }
}
