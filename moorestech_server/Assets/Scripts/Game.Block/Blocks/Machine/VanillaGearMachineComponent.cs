using Game.Block.Blocks.Gear;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.EnergySystem;
using Game.Gear.Common;
using Mooresmaster.Model.BlocksModule;
using UniRx;
using UnityEngine;

namespace Game.Block.Blocks.Machine
{
    /// <summary>
    ///     歯車機械を表すクラス
    ///     Class representing a gear-powered machine
    /// </summary>
    public class VanillaGearMachineComponent : IBlockComponent
    {
        private readonly GearEnergyTransformer _gearEnergyTransformer;
        private readonly VanillaMachineProcessorComponent _vanillaMachineProcessorComponent;
        private readonly float _blockDefaultTorque;
        private readonly float _blockDefaultRpm;

        public VanillaGearMachineComponent(VanillaMachineProcessorComponent vanillaMachineProcessorComponent, GearEnergyTransformer gearEnergyTransformer, GearMachineBlockParam gearMachineBlockParam)
        {
            _vanillaMachineProcessorComponent = vanillaMachineProcessorComponent;
            _gearEnergyTransformer = gearEnergyTransformer;
            _blockDefaultTorque = gearMachineBlockParam.RequireTorque;
            _blockDefaultRpm = gearMachineBlockParam.RequiredRpm;

            _gearEnergyTransformer.OnGearUpdate.Subscribe(OnGearUpdate);
        }

        private void OnGearUpdate(GearUpdateType gearUpdateType)
        {
            // 現在のレシピからオーバーライドされたRPM/Torqueを取得
            // Get overridden RPM/Torque from current recipe
            var torque = _blockDefaultTorque;
            var rpm = _blockDefaultRpm;
            RecipeEnergyOverrideResolver.ResolveGearParams(_vanillaMachineProcessorComponent.CurrentRecipe, ref torque, ref rpm);

            var requiredRpm = new RPM(rpm);
            var requireTorque = new Torque(torque);

            var currentElectricPower = _gearEnergyTransformer.CalcMachineSupplyPower(requiredRpm, requireTorque);
            _vanillaMachineProcessorComponent.SupplyPower(currentElectricPower);
        }

        public bool IsDestroy { get; private set; }
        public void Destroy()
        {
            BlockException.CheckDestroy(this);
            IsDestroy = true;
        }
    }
}
