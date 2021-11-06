namespace Core.Item
{
    public class ItemProcessResult
    {
        public ItemProcessResult(IItemStack mineItemStack, IItemStack receiveItemStack)
        {
            MineItemStack = mineItemStack;
            ReceiveItemStack = receiveItemStack;
        }

        public IItemStack ReceiveItemStack { get; }

        public IItemStack MineItemStack { get; }
    }
}