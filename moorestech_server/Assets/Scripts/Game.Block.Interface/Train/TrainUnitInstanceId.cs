using System;
using UnitGenerator;

namespace Game.Train.Unit
{
    [UnitOf(typeof(Guid), UnitGenerateOptions.MessagePackFormatter)]
    public readonly partial struct TrainUnitInstanceId
    {
        public static TrainUnitInstanceId Create()
        {
            return new TrainUnitInstanceId(Guid.NewGuid());
        }
    }
}
