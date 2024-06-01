namespace Core.Item.Interface
{
    public class ItemProcessResult
    {
        public ItemProcessResult(IItemStack processResultItemStack, IItemStack remainderItemStack)
        {
            ProcessResultItemStack = processResultItemStack;
            RemainderItemStack = remainderItemStack;
        }

        /// <summary>
        ///     処理した結果余ったアイテムスタック
        /// </summary>
        public IItemStack RemainderItemStack { get; }

        /// <summary>
        ///     元のアイテムスタックに対する処理結果のアイテムスタック
        /// </summary>
        public IItemStack ProcessResultItemStack { get; }
    }
}