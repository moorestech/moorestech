using System.Collections.Generic;
using Core.Item;
using Core.Item.Implementation;

namespace Core.Block.RecipeConfig.Data
{

    public class ItemOutput
    {
        public IItemStack OutputItem { get; }

        public double Percent { get; }

        public ItemOutput(IItemStack outputItemMachine, double percent)
        {
            OutputItem = outputItemMachine;
            Percent = percent;
        }
    }
}