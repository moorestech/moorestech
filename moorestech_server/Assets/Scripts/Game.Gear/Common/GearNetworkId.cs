using Core.Update;
using UnitGenerator;

namespace Game.Gear.Common
{
    [UnitOf(typeof(int))]
    public readonly partial struct GearNetworkId
    {
        public static GearNetworkId CreateNetworkId()
        {
            // intの最小から最大までの乱数を生成
            return new GearNetworkId(GameRandom.NextInt());
        }
    }
}
