using System;

namespace Core.Util
{
    public static class RandomExtension
    {
        public static long NextInt64(this Random random, long min, long max)
        {
            long result = random.Next((Int32) (min >> 32), (Int32) (max >> 32));
            result = (result << 32);
            result |= (long) random.Next((Int32) min, (Int32) max);
            return result;
        }
    }
}