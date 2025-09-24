using System;
using Game.Block.Blocks.Gear;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.EnergySystem;
using Game.Gear.Common;
using MessagePack;
using Mooresmaster.Model.BlocksModule;
using UnityEngine;

namespace Game.Block.Blocks.GearElectric
{
    public class GearElectricGeneratorComponent : GearEnergyTransformer, IGear, IElectricGenerator, IUpdatableBlockComponent, IBlockStateDetail
    {
        public int TeethCount => _param.TeethCount;
        public float EnergyFulfillmentRate { get; private set; }
        
        
        private readonly GearElectricGeneratorBlockParam _param;
        private ElectricPower _currentGeneratedPower;
        
        public GearElectricGeneratorComponent(
            GearElectricGeneratorBlockParam param,
            BlockInstanceId blockInstanceId,
            IBlockConnectorComponent<IGearEnergyTransformer> connectorComponent) :
            base(new Torque(param.RequiredTorque), blockInstanceId, connectorComponent)
        {
            _param = param;
            _currentGeneratedPower = new ElectricPower(0);
            EnergyFulfillmentRate = 0f;
        }
        
        public void Update()
        {
            BlockException.CheckDestroy(this);
            
            UpdateGeneratedPower();
        }
        
        private void UpdateGeneratedPower()
        {
            var requiredRpm = _param.RequiredRpm;
            var requiredTorque = RequiredTorque.AsPrimitive();
            var currentRpm = CurrentRpm.AsPrimitive();
            var currentTorque = CurrentTorque.AsPrimitive();
            
            if (requiredRpm <= 0f || requiredTorque <= 0f || currentRpm <= 0f || currentTorque <= 0f)
            {
                SetState(0f, 0f);
                return;
            }
            
            var rpmRate = Mathf.Min(currentRpm / requiredRpm, 1f);
            var torqueRate = Mathf.Min(currentTorque / requiredTorque, 1f);
            var fulfillment = Mathf.Clamp01(Mathf.Min(rpmRate, torqueRate));
            
            SetState(_param.MaxGeneratedPower * fulfillment, fulfillment);
            
            #region Internal
            
            void SetState(float generatedPower, float fulfillmentRate)
            {
                _currentGeneratedPower = new ElectricPower(generatedPower);
                EnergyFulfillmentRate = fulfillmentRate;
            }
            
            #endregion
        }
        
        public new BlockStateDetail[] GetBlockStateDetails()
        {
            BlockException.CheckDestroy(this);
            
            var baseDetails = base.GetBlockStateDetails();
            var result = new BlockStateDetail[baseDetails.Length + 1];
            result[0] = CreateDetail();
            Array.Copy(baseDetails, 0, result, 1, baseDetails.Length);
            return result;
            
            #region Internal
            
            BlockStateDetail CreateDetail()
            {
                var network = GearNetworkDatastore.GetGearNetwork(BlockInstanceId);
                var detail = new GearElectricGeneratorBlockStateDetail(
                    IsCurrentClockwise,
                    CurrentRpm,
                    CurrentTorque,
                    network.CurrentGearNetworkInfo,
                    EnergyFulfillmentRate,
                    _currentGeneratedPower);
                var serialized = MessagePackSerializer.Serialize(detail);
                return new BlockStateDetail(GearElectricGeneratorBlockStateDetail.GearGeneratorBlockStateDetailKey, serialized);
            }
            
            #endregion
        }
        
        
        public ElectricPower OutputEnergy()
        {
            BlockException.CheckDestroy(this);
            return _currentGeneratedPower;
        }
    }
}
