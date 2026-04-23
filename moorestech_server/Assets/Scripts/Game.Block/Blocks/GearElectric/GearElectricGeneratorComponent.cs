using System;
using Game.Block.Blocks.Gear;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.EnergySystem;
using Game.Gear.Common;
using MessagePack;
using Mooresmaster.Model.BlocksModule;

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
            base(param.GearConsumption, blockInstanceId, connectorComponent)
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
            // 稼働率（RPM比 × torqueRate）をそのまま発電充足率として用いる。1超もOK（高RPMで発電増加）
            // Use operating rate (rpmRatio × torqueRate) directly as fulfillment. >1 is allowed for high-RPM overdrive
            var fulfillment = CurrentOperatingRate;
            if (fulfillment <= 0f)
            {
                SetState(0f, 0f);
                return;
            }

            SetState((float)_param.MaxGeneratedPower * fulfillment, fulfillment);

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
                var detail = new GearElectricGeneratorBlockStateDetail(
                    IsCurrentClockwise,
                    CurrentRpm,
                    CurrentTorque,
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
