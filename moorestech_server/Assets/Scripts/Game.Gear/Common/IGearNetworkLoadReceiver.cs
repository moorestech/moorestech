namespace Game.Gear.Common
{
    public interface IGearNetworkLoadReceiver
    {
        void UpdateByGearNetworkLoadRate(float networkLoadRate);
    }
}
