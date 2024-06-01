using System.Collections.Generic;
using Core.Item.Interface;
using Game.Context;

namespace Core.Item.Util
{
    public static class CreateEmptyItemStacksList
    {
        public static List<IItemStack> Create(int count)
        {
            var a = new List<IItemStack>();
            for (var i = 0; i < count; i++) a.Add(ServerContext.ItemStackFactory.CreatEmpty());
            
            return a;
        }
    }
}