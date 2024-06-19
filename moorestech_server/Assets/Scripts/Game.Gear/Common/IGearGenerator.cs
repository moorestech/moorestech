namespace Game.Gear.Common
{
    public interface IGearGenerator : IGear
    {
        public RPM GenerateRpm { get; }
        public float GenerateTorque { get; }
        
        public bool GenerateIsClockwise { get; }
    }
}