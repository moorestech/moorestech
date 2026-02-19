using System;
using UnitGenerator;

namespace Game.Train.Unit
{
    [UnitOf(typeof(Guid), UnitGenerateOptions.MessagePackFormatter)]
    public readonly partial struct TrainInstanceId
    {
        public static TrainInstanceId Create()
        {
            return new TrainInstanceId(Guid.NewGuid());
        }
    }
}
