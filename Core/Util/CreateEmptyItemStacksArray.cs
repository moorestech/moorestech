using System.Collections.Generic;
using Core.Item;

namespace Core.Util
{
    public static class CreateEmptyItemStacksList
    {
        public static List<IItemStack> Create(int amount)
        {
            var a = new List<IItemStack>();
            for (var i = 0; i < amount; i++)
            {
                a.Add(new NullItemStack());
            }
            return a;
        }
    }
}