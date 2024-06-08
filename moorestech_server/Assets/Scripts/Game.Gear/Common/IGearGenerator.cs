namespace Game.Gear.Common
{
    public interface IGearGenerator : IGear
    {
        public float GenerateRpm { get; }
        public float GenerateTorque { get; }
        
        public bool GenerateIsClockwise { get; }
    }
}