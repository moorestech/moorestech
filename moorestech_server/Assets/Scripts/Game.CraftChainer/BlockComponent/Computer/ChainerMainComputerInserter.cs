using Core.Item.Interface;
using Game.Block.Blocks.Connector;

namespace Game.CraftChainer.BlockComponent.Computer
{
    /// <summary>
    /// メインコンピューターはアイテムを外に出さないので、そのためのクラス
    /// The main computer does not take items out, so this class is for that
    /// </summary>
    public class ChainerMainComputerInserter : IBlockInventoryInserter
    {
        public IItemStack InsertItem(IItemStack itemStack)
        {
            return itemStack;
        }
    }
}