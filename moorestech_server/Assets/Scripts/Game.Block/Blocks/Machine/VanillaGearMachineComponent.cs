using System.Collections.Generic;
using System.Linq;
using Game.Block.Blocks.Gear;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.EnergySystem;
using Game.Gear.Common;
using Mooresmaster.Model.BlockConnectInfoModule;
using Mooresmaster.Model.BlocksModule;
using UnityEngine;

namespace Game.Block.Blocks.Machine
{
    /// <summary>
    ///     歯車機械を表すクラス
    /// </summary>
    public class VanillaGearMachineComponent : IGear
    {
        public Torque CurrentTorque => _simpleGearService.CurrentTorque;
        public int TeethCount => _gearMachineBlockParam.TeethCount;
        public BlockInstanceId BlockInstanceId { get; }
        public RPM CurrentRpm => _simpleGearService.CurrentRpm;
        public bool IsCurrentClockwise => _simpleGearService.IsCurrentClockwise;
        public bool IsRocked => _simpleGearService.IsRocked;
        
        private readonly GearMachineBlockParam _gearMachineBlockParam;
        private readonly IBlockConnectorComponent<IGearEnergyTransformer> _connectorComponent;
        
        private readonly SimpleGearService _simpleGearService;
        private readonly VanillaMachineProcessorComponent _vanillaMachineProcessorComponent;
        public VanillaGearMachineComponent(GearMachineBlockParam gearMachineBlockParam, VanillaMachineProcessorComponent vanillaMachineProcessorComponent, IBlockConnectorComponent<IGearEnergyTransformer> connectorComponent, BlockInstanceId blockInstanceId)
        {
            _gearMachineBlockParam = gearMachineBlockParam;
            BlockInstanceId = blockInstanceId;
            _vanillaMachineProcessorComponent = vanillaMachineProcessorComponent;
            _connectorComponent = connectorComponent;
            _simpleGearService = new SimpleGearService();
        }
        
        public IReadOnlyList<GearConnect> Connects =>
            _connectorComponent.ConnectedTargets.Select(
                target => new GearConnect(target.Key, (GearConnectOption)target.Value.selfOption, (GearConnectOption)target.Value.targetOption)
            ).ToArray();
        
        public Torque GetRequiredTorque(RPM rpm, bool isClockwise)
        {
            BlockException.CheckDestroy(this);
            return _vanillaMachineProcessorComponent.CurrentState is ProcessState.Processing ? new Torque(_gearMachineBlockParam.RequireTorque) : new Torque(0);
        }
        
        public void Rocked()
        {
            BlockException.CheckDestroy(this);
            _simpleGearService.Rocked();
            _vanillaMachineProcessorComponent.SupplyPower(new ElectricPower(0));
        }
        
        public bool IsDestroy { get; private set; }
        public void Destroy()
        {
            BlockException.CheckDestroy(this);
            IsDestroy = true;
        }
        
        public void SupplyPower(RPM rpm, Torque torque, bool isClockwise)
        {
            BlockException.CheckDestroy(this);
            _simpleGearService.SupplyPower(rpm, torque, isClockwise);
            
            var rpmRate = Mathf.Min((rpm / _gearMachineBlockParam.RequiredRpm).AsPrimitive(), 1);
            var torqueRate = Mathf.Min((torque / _gearMachineBlockParam.RequireTorque).AsPrimitive(), 1);
            var powerRate = rpmRate * torqueRate;
            
            var requiredGearPower = _gearMachineBlockParam.RequiredRpm * _gearMachineBlockParam.RequireTorque;
            var currentElectricPower = new ElectricPower(requiredGearPower * powerRate);
            _vanillaMachineProcessorComponent.SupplyPower(currentElectricPower);
        }
        
        public List<GearConnect> GetGearConnects()
        {
            var result = new List<GearConnect>();
            foreach (var target in _connectorComponent.ConnectedTargets)
            {
                result.Add(new GearConnect(target.Key, (GearConnectOption)target.Value.selfOption, (GearConnectOption)target.Value.targetOption));
            }
            return result;
        }
    }
}