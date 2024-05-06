namespace Game.Gear.Common
{
    public interface IGearConsumer : IGear
    {
        public float RequiredPower { get; }
        public void SupplyPower(float rpm, float torque, bool isClockwise);
    }
}