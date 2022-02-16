using System.Collections.Generic;
using Core.Item;

namespace Game.Crafting.Interface
{
    public interface IIsCreatableJudgementService
    {
        public bool IsCreatable(List<IItemStack> craftingItems);
    }
}