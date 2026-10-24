using Game.Block.Interface;
using Game.EnergySystem;

namespace Game.Block.Blocks.SpaceElevator
{
    // 所属セグメントの確定済み供給率から実効電力を導き、昇降機へ渡す
    // Derives effective power from the segment's settled supply rate and feeds the climber
    public class SpaceElevatorBaseElectricComponent : IElectricConsumer, IElectricTickPostHandler
    {
        public BlockInstanceId BlockInstanceId { get; }
        public ElectricPower RequestEnergy => new(_climber.RequestPower);

        private readonly SpaceElevatorClimberComponent _climber;

        public SpaceElevatorBaseElectricComponent(BlockInstanceId blockInstanceId, SpaceElevatorClimberComponent climber)
        {
            BlockInstanceId = blockInstanceId;
            _climber = climber;
        }

        public void OnElectricTickPostProcess(ElectricNetworkStatistics statistics)
        {
            BlockException.CheckDestroy(this);
            _climber.SupplyPower(RequestEnergy.AsPrimitive() * statistics.PowerRate);
        }

        public bool IsDestroy { get; private set; }
        public void Destroy()
        {
            IsDestroy = true;
        }
    }
}
