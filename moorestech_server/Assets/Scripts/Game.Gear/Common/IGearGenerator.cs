namespace Game.Gear.Common
{
    public interface IGearGenerator : IGear
    {
        public float GeneratePower => GenerateRpm * GenerateTorque;
        public float GenerateRpm { get; }
        public float GenerateTorque { get; }
        
        public bool GenerateIsClockwise { get; }
    }
}