using UnitGenerator;
using UnityEngine;

namespace Game.Gear.Common
{
    [UnitOf(typeof(int))]
    public readonly partial struct GearNetworkId
    {
        public static GearNetworkId CreateNetworkId()
        {
            return new GearNetworkId(Random.Range(int.MinValue, int.MaxValue));
        }
    }
}