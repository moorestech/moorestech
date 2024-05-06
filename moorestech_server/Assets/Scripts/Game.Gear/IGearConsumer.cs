namespace Game.Gear
{
    public interface IGearConsumer : IGearComponent
    {
        public float RequiredPower { get; }
        public void SupplyPower(float rpm, float torque, bool isClockwise);
    }
}