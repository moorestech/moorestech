namespace Game.Gear.Common
{
    public interface IGearGenerator : IGear
    {
        public RPM GenerateRpm { get; }
        public Torque GenerateTorque { get; }
        
        public bool GenerateIsClockwise { get; }
    }
}