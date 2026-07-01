namespace Game.Gear.Common
{
    public interface IGearTickFuelConsumer
    {
        void UpdateFromGearTick(float networkLoadRate);
    }
}
