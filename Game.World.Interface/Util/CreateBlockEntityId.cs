using System;

namespace Game.World.Interface.Util
{
    public static class CreateBlockEntityId
    {
        private static readonly Random Random = new Random();

        public static int Create()
        {
            return Random.Next(Int32.MinValue, Int32.MaxValue);
        }
    }
}