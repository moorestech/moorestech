using System;
using Game.Block.Blocks.Gear;
using Game.Block.Component;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Gear.Common;
using Mooresmaster.Model.BlocksModule;
using UnityEngine;

namespace Game.Block.Blocks.ItemShooter
{
    public class ItemShooterAcceleratorComponent : GearEnergyTransformer, IUpdatableBlockComponent
    {
        private readonly ItemShooterComponentService _service;
        private readonly ItemShooterAcceleratorBlockParam _param;
        private readonly Torque _requiredTorque;
        private readonly RPM _requiredRpm;
        private readonly float _maxMultiplier;

        public ItemShooterAcceleratorComponent(
            ItemShooterComponentService service,
            ItemShooterAcceleratorBlockParam param,
            BlockInstanceId blockInstanceId,
            BlockConnectorComponent<IGearEnergyTransformer> gearConnector)
            : base(new Torque((float)param.RequireTorque), blockInstanceId, gearConnector)
        {
            _service = service;
            _param = param;
            _requiredTorque = new Torque((float)param.RequireTorque);
            _requiredRpm = new RPM((float)param.RequiredRpm);
            _maxMultiplier = (float)Math.Max(param.MaxAccelerationMultiplier, 0);
        }

        public void Update()
        {
            BlockException.CheckDestroy(this);
            _service.SetExternalAcceleration(CalculateAcceleration());
        }

        private float CalculateAcceleration()
        {
            var suppliedTorque = CurrentTorque.AsPrimitive();
            var suppliedRpm = CurrentRpm.AsPrimitive();

            if (suppliedTorque < _requiredTorque.AsPrimitive()) return 0f;

            var requiredRpm = Mathf.Max(_requiredRpm.AsPrimitive(), 0.0001f);
            var rpmRatio = suppliedRpm / requiredRpm;
            if (rpmRatio < 1f) return 0f;

            var multiplier = _maxMultiplier > 0f ? Mathf.Min(rpmRatio, _maxMultiplier) : rpmRatio;
            return (float)_param.PoweredAcceleration * multiplier;
        }
    }
}
