namespace Core.Item.Util
{
    public class ItemProcessResult
    {
        public ItemProcessResult(IItemStack processResultItemStack, IItemStack remainderItemStack)
        {
            ProcessResultItemStack = processResultItemStack;
            RemainderItemStack = remainderItemStack;
        }


        ///     

        public IItemStack RemainderItemStack { get; }


        ///     

        public IItemStack ProcessResultItemStack { get; }
    }
}