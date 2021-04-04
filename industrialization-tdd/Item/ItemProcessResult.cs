namespace industrialization.Item
{
    public class ItemProcessResult
    {
        private IItemStack mineItemStack;
        private IItemStack receiveItemStack;


        public ItemProcessResult(IItemStack mineItemStack, IItemStack receiveItemStack)
        {
            this.mineItemStack = mineItemStack;
            this.receiveItemStack = receiveItemStack;
        }

        public IItemStack ReceiveItemStack => receiveItemStack;

        public IItemStack MineItemStack => mineItemStack;
    }
}