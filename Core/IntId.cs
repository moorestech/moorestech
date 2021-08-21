using System;

namespace industrialization.Core
{
    public static class IntId
    {
        private static Random _random = new Random();
        public static uint NewIntId()
        {
            return (uint)(_random.Next(1,Int32.MaxValue) + _random.Next(1,Int32.MaxValue-2));
        }
    }
}