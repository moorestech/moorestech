using System;
using UnitGenerator;

namespace Game.Train.Unit
{
    [UnitOf(typeof(Guid), UnitGenerateOptions.MessagePackFormatter | UnitGenerateOptions.Comparable)]
    public readonly partial struct TrainInstanceId
    {
        public static TrainInstanceId Empty => new(Guid.Empty);

        public static TrainInstanceId Create()
        {
            return new TrainInstanceId(Guid.NewGuid());
        }
    }
}