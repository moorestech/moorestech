using Server.Core.Item;

namespace Game.Block.Interface.RecipeConfig
{
    public class ItemOutput
    {
        public ItemOutput(IItemStack outputItemMachine, double percent)
        {
            OutputItem = outputItemMachine;
            Percent = percent;
        }

        public IItemStack OutputItem { get; }

        public double Percent { get; }
    }
}