using System;
using UnitGenerator;

namespace Game.Block.Interface
{
    [UnitOf(typeof(int), UnitGenerateOptions.MessagePackFormatter | UnitGenerateOptions.Comparable)]
    public readonly partial struct BlockInstanceId
    {
        private static readonly Random Random = new();
        
        public static BlockInstanceId Create()
        {
            return new BlockInstanceId(Random.Next(int.MinValue, int.MaxValue));
        }
    }
}