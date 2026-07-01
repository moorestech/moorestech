using System.Collections.Generic;

namespace Game.Gear.Common
{
    public static class GearNetworkFuelUpdater
    {
        public static void UpdateFuelGenerators(IReadOnlyList<IGearGenerator> generators, float networkLoadRate)
        {
            foreach (var generator in generators)
            {
                if (generator is IGearNetworkLoadReceiver receiver)
                {
                    receiver.UpdateByGearNetworkLoadRate(networkLoadRate);
                }
            }
        }
    }
}
