using System;
using UnitGenerator;

namespace Game.Block.Interface
{
    [UnitOf(typeof(int))]
    public readonly partial struct EntityID
    {
        private static readonly Random Random = new(130851);
        
        public static EntityID Create()
        {
            return new EntityID(Random.Next(int.MinValue, int.MaxValue));
        }
    }
}