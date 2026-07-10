using Game.Block.Blocks.Gear;
using Game.Block.Interface.Component;
using UniRx;

namespace Game.Block.Blocks.Miner
{
    public class VanillaGearMinerComponent : IBlockComponent
    {
        private readonly GearEnergyTransformer _gearEnergyTransformer;
        private readonly VanillaMinerProcessorComponent _vanillaMinerProcessorComponent;
        private readonly float _idleTorqueRate;

        public VanillaGearMinerComponent(VanillaMinerProcessorComponent vanillaMinerProcessorComponent, GearEnergyTransformer gearEnergyTransformer, float idleTorqueRate)
        {
            _vanillaMinerProcessorComponent = vanillaMinerProcessorComponent;
            _gearEnergyTransformer = gearEnergyTransformer;
            _idleTorqueRate = idleTorqueRate;
            _gearEnergyTransformer.OnGearUpdate.Subscribe(OnGearUpdate);

            // 採掘状態の変化に応じて要求トルク倍率を変更要求する
            // Push the torque request rate whenever the mining state changes
            _vanillaMinerProcessorComponent.OnChangeBlockState.Subscribe(_ => UpdateTorqueRequestRate());
            UpdateTorqueRequestRate();
        }

        private void OnGearUpdate(GearUpdateType gearUpdateType)
        {
            _vanillaMinerProcessorComponent.SupplyPower(_gearEnergyTransformer.GetCurrentSuppliedPower().AsPrimitive());
        }

        private void UpdateTorqueRequestRate()
        {
            _gearEnergyTransformer.SetTorqueRequestRate(_vanillaMinerProcessorComponent.IsMining ? 1f : _idleTorqueRate);
        }

        public bool IsDestroy { get; private set; }
        public void Destroy()
        {
            IsDestroy = true;
        }
    }
}
