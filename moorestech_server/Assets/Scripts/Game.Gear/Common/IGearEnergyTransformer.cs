using System.Collections.Generic;
using Game.Block.Interface;
using Game.Block.Interface.Component;

namespace Game.Gear.Common
{
    public interface IGearEnergyTransformer : IBlockComponent
    {
        public const string WorkingStateName = "Working";
        public const string RockedStateName = "Rocked";
        
        public BlockInstanceId BlockInstanceId { get; }
        
        public bool IsRocked { get; }
        
        public GearPower CurrentPower => new(CurrentRpm.AsPrimitive() * CurrentTorque.AsPrimitive());
        public RPM CurrentRpm { get; }
        public Torque CurrentTorque { get; }
        public bool IsCurrentClockwise { get; }
        
        public IReadOnlyList<GearConnect> Connects { get; }
        
        public Torque GetRequiredTorque(RPM rpm, bool isClockwise);
        
        public void Rocked();
        public void SupplyPower(RPM rpm, Torque torque, bool isClockwise);
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
}