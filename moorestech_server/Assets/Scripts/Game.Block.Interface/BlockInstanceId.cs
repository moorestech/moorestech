using Core.Update;
using UnitGenerator;

namespace Game.Block.Interface
{
    [UnitOf(typeof(int), UnitGenerateOptions.MessagePackFormatter | UnitGenerateOptions.Comparable)]
    public readonly partial struct BlockInstanceId
    {
        public static BlockInstanceId Create()
        {
            return new BlockInstanceId(GameRandom.NextInt());
        }
    }
}
