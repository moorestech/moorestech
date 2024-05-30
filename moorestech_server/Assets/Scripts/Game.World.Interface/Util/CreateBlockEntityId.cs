using System;

namespace Game.World.Interface.Util
{
    public static class CreateBlockEntityId
    {
        private static readonly Random Random = new();
        
        public static int Create()
        {
            return Random.Next(int.MinValue, int.MaxValue);
        }
    }
}