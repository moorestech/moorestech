namespace Game.Gear.Common
{
    public interface IGear : IGearEnergyTransformer
    {
        public int TeethCount { get; }
    }
}