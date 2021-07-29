using System;

namespace industrialization.Core
{
    public static class IntId
    {
        private static Random _random = new Random();
        public static int NewIntId()
        {
            return _random.Next(Int32.MinValue,Int32.MaxValue);
        }
    }
}