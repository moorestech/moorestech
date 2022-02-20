using System;

namespace Game.World.Interface.Util
{
    public static class EntityId
    {
        private static readonly Random Random = new Random();

        public static int NewEntityId()
        {
            return Random.Next(Int32.MinValue, Int32.MaxValue);
        }
    }
}