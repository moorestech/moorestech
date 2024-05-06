namespace Game.Gear
{
    public interface IGearComponent
    {
        public int RequestPower { get; }
        public int GeneratePower { get; }
    }
}