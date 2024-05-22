using System.Collections.Generic;
using Game.Block.Interface.Component;

namespace Game.Gear.Common
{
    public interface IGearEnergyTransformer : IBlockComponent
    {
        public const string WorkingStateName = "Working";
        public const string RockedStateName = "Rocked";

        public int EntityId { get; }
        public float RequiredPower { get; }

        public float CurrentRpm { get; }
        public float CurrentTorque { get; }
        public bool IsCurrentClockwise { get; }

        public IReadOnlyList<GearConnect> Connects { get; }

        public void Rocked();
        public void SupplyPower(float rpm, float torque, bool isClockwise);
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