using System.Collections.Generic;
using Game.Gear.Common;

namespace Game.Gear.Component
{
    public class GearComponent : IGearConsumer
    {
        public IReadOnlyList<IGear> ConnectingTransformers { get; }
        public int TeethCount { get; }
        public int EntityId { get; }
        public float RequiredPower { get; }
        public bool IsDestroy { get; private set; }

        public void Destroy()
        {
            IsDestroy = true;
        }
        public void SupplyPower(float rpm, float torque, bool isClockwise)
        {
            throw new System.NotImplementedException();
        }
    }
}