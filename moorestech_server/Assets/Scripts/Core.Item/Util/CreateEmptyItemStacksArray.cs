using System.Collections.Generic;
using Core.Item.Interface;

namespace Core.Item.Util
{
    public static class CreateEmptyItemStacksList
    {
        public static List<IItemStack> Create(int count, IItemStackFactory itemStackFactory)
        {
            var a = new List<IItemStack>();
            for (var i = 0; i < count; i++) a.Add(itemStackFactory.CreatEmpty());

            return a;
        }
    }
}