using System.Collections.Generic;
using Game.Block.Interface.Component;
using Game.Gear.Common;

namespace Game.Gear.Component
{
    public class GearComponent : IGearConsumer
    {
        public IReadOnlyList<IGearEnergyTransformer> ConnectingTransformers => _connectorComponent.ConnectTargets;
        public int EntityId { get; }
        public int TeethCount { get; }
        public float RequiredPower { get; }
        public bool IsDestroy { get; private set; }

        private readonly IBlockConnectorComponent<IGearEnergyTransformer> _connectorComponent;

        public GearComponent(IBlockConnectorComponent<IGearEnergyTransformer> connectorComponent, int entityId, int teethCount, float requiredPower)
        {
            _connectorComponent = connectorComponent;
            EntityId = entityId;
            TeethCount = teethCount;
            RequiredPower = requiredPower;
        }

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