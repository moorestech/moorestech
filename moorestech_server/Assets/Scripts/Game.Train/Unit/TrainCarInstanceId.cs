using System;
using UnitGenerator;

namespace Game.Train.Unit
{
    [UnitOf(typeof(long), UnitGenerateOptions.MessagePackFormatter | UnitGenerateOptions.Comparable)]
    public readonly partial struct TrainCarInstanceId
    {
        private static readonly Random Random = new();
        
        public static TrainCarInstanceId Create()
        {
            long result = Random.Next(int.MinValue, int.MaxValue);
            result <<= 32;
            result |= (uint)Random.Next(int.MinValue, int.MaxValue);
            return new TrainCarInstanceId(result);
        }
    }
}
