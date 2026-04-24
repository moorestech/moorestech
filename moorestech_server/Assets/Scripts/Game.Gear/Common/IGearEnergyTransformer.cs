using System.Collections.Generic;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Mooresmaster.Model.BlockConnectInfoModule;

namespace Game.Gear.Common
{
    public interface IGearEnergyTransformer : IBlockComponent
    {
        public const string WorkingStateName = "Working";
        public const string RockedStateName = "Rocked";
        
        public BlockInstanceId BlockInstanceId { get; }
        
        public GearPower CurrentPower => new(CurrentRpm.AsPrimitive() * CurrentTorque.AsPrimitive());
        public RPM CurrentRpm { get; }
        public Torque CurrentTorque { get; }
        public bool IsCurrentClockwise { get; }

        // 隣接エッジを通過するトルクの最大値（Laplacian解から算出）。過負荷破壊判定用
        // Max torque flowing across adjacent edges (from Laplacian solve). Used for overload breakage
        public Torque CurrentLoadTorque { get; }

        public Torque GetRequiredTorque(RPM rpm, bool isClockwise);

        public void StopNetwork();
        public void SupplyPower(RPM rpm, Torque torque, bool isClockwise);
        public void SetCurrentLoadTorque(Torque loadTorque);

        public List<GearConnect> GetGearConnects();
    }
    
    public readonly struct GearConnect
    {
        public readonly IGearEnergyTransformer Transformer;
        public readonly GearConnectOption Self;
        public readonly GearConnectOption Target;
        
        public GearConnect(IGearEnergyTransformer transformer, GearConnectOption self, GearConnectOption target)
        {
            Transformer = transformer;
            Self = self;
            Target = target;
        }
    }
    
    public readonly struct GearNetworkInfo
    {
        public readonly float TotalRequiredGearPower;
        public readonly float TotalGenerateGearPower;
        public readonly float OperatingRate;
        public readonly GearNetworkStopReason StopReason;

        public GearNetworkInfo(float totalRequiredGearPower, float totalGenerateGearPower, float operatingRate, GearNetworkStopReason stopReason)
        {
            TotalRequiredGearPower = totalRequiredGearPower;
            TotalGenerateGearPower = totalGenerateGearPower;
            OperatingRate = operatingRate;
            StopReason = stopReason;
        }

        public static GearNetworkInfo CreateEmpty()
        {
            return new GearNetworkInfo(0, 0, 0, GearNetworkStopReason.None);
        }
    }
}