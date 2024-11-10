using System;
using UnitGenerator;

namespace Core.Item.Interface
{
    [UnitOf(typeof(long))]
    public partial struct ItemInstanceId
    {
        //TODO randomを全て一つのシードから生成するようにする
        private static readonly Random Random = new();
        
        public static ItemInstanceId Create()
        {
            long result = Random.Next(int.MinValue, int.MaxValue);
            result <<= 32;
            result |= (uint)Random.Next(int.MinValue, int.MaxValue);
            return new ItemInstanceId(result);
        }
    }
}