
using System;
using Core.Util;

namespace Core.Item
{
    public static class ItemInstanceIdGenerator
    {
        //TODO randomを全て一つのシードから生成するようにする
        private static readonly Random Random = new Random(1);

        public static long Generate()
        {
            return 0;
        }

    }
}