using System;
using UnitGenerator;

namespace Game.Gear.Common
{
    [UnitOf(typeof(int))]
    public readonly partial struct GearNetworkId
    {
        private static readonly Random Random = new(123456);
        
        public static GearNetworkId CreateNetworkId()
        {
            // intの最小から最大までの乱数を生成
            return new GearNetworkId(Random.Next(int.MinValue, int.MaxValue));
        }
    }
}