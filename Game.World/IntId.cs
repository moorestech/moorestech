using System;

namespace World
{
    public static class IntId
    {
        private static readonly Random Random = new Random();
        public static int NewIntId()
        {
            return Random.Next(Int32.MinValue,Int32.MaxValue);
        }
    }
}