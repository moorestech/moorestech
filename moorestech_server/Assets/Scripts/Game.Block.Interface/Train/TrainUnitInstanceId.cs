using System;
using Core.Update;
using UnitGenerator;

namespace Game.Train.Unit
{
    [UnitOf(typeof(Guid), UnitGenerateOptions.MessagePackFormatter)]
    public readonly partial struct TrainUnitInstanceId
    {
        public static TrainUnitInstanceId Create()
        {
            return new TrainUnitInstanceId(GameRandom.NextGuid());
        }
    }
}
