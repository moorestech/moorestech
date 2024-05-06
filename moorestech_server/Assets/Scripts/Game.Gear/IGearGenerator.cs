namespace Game.Gear
{
    public interface IGearGenerator : IGearComponent
    {
        public float GeneratePower => GenerateRpm * GenerateTorque;
        public float GenerateRpm { get; }
        public float GenerateTorque { get; }

        public bool IsClockwise { get; }
    }
}