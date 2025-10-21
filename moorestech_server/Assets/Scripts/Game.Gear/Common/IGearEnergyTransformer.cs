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
        
        public bool IsRocked { get; }
        public GearNetworkStopReason StopReason { get; }
        
        public GearPower CurrentPower => new(CurrentRpm.AsPrimitive() * CurrentTorque.AsPrimitive());
        public RPM CurrentRpm { get; }
        public Torque CurrentTorque { get; }
        public bool IsCurrentClockwise { get; }
        
        public Torque GetRequiredTorque(RPM rpm, bool isClockwise);
        
        public void StopNetwork(GearNetworkStopReason reason);
        public void SupplyPower(RPM rpm, Torque torque, bool isClockwise);
        
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
        
        public GearNetworkInfo(float totalRequiredGearPower, float totalGenerateGearPower, float operatingRate)
        {
            TotalRequiredGearPower = totalRequiredGearPower;
            TotalGenerateGearPower = totalGenerateGearPower;
            OperatingRate = operatingRate;
        }
        
        public static GearNetworkInfo CreateEmpty()
        {
            return new GearNetworkInfo(0, 0, 0);
        }
    }
}