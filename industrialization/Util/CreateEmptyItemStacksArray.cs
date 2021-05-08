using System.Collections.Generic;
using industrialization.Item;

namespace industrialization.Util
{
    public class CreateEmptyItemStacksList
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