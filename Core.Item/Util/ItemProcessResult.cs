namespace Core.Item.Util
{
    public class ItemProcessResult
    {
        public ItemProcessResult(IItemStack processRemainderItemStack, IItemStack remainderItemStack)
        {
            ProcessRemainderItemStack = processRemainderItemStack;
            RemainderItemStack = remainderItemStack;
        }

        /// <summary>
        /// 元のアイテムスタックに対する処理結果のアイテムスタック
        /// </summary>
        public IItemStack RemainderItemStack { get; }

        /// <summary>
        /// 処理した結果余ったアイテムスタック
        /// </summary>
        public IItemStack ProcessRemainderItemStack { get; }
    }
}