using Core.Update;
using UnitGenerator;

namespace Game.Train.Unit
{
    [UnitOf(typeof(long), UnitGenerateOptions.MessagePackFormatter | UnitGenerateOptions.Comparable)]
    public readonly partial struct TrainCarInstanceId
    {
        public static TrainCarInstanceId Create()
        {
            return new TrainCarInstanceId(GameRandom.NextLong());
        }
    }
}
