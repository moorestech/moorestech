namespace Game.Gear.Common
{
    public interface IGear : IGearEnergyTransformer
    {
        bool IGearEnergyTransformer.IsReverseRotation => true;

        public int TeethCount { get; }
    }
}