using System;

namespace Core.Item
{
    public static class ItemInstanceIdGenerator
    {
        //TODO random
        private static readonly Random Random = new(1);

        public static long Generate()
        {
            long result = Random.Next(int.MinValue, int.MaxValue);
            result <<= 32;
            result |= (uint)Random.Next(int.MinValue, int.MaxValue);
            return result;
        }
    }
}