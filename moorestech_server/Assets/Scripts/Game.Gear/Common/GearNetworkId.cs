using Game.Block.Interface;
using UnitGenerator;

namespace Game.Gear.Common
{
    [UnitOf(typeof(int))]
    public readonly partial struct GearNetworkId
    {
        // 連結成分の最小ブロックIDから導出する。tick毎の再構築で乱数を引くと、ロード直後の再構築だけが乱数列を余分に進め再生が一致しなくなる
        // Derive it from the component's smallest block id; drawing randomness per rebuild would advance the stream only on the post-load rebuild and break replay
        public static GearNetworkId FromComponentRootBlock(BlockInstanceId rootBlockInstanceId)
        {
            return new GearNetworkId(rootBlockInstanceId.AsPrimitive());
        }
    }
}
