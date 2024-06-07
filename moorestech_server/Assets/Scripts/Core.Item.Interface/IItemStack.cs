namespace Core.Item.Interface
{
    public interface IItemStack
    {
        int Id { get; }
        int Count { get; }
        long ItemHash { get; }
        
        /// <summary>
        ///     アイテムを識別するID
        ///     新しいインスタンスが生成されるたびにかわる
        ///     基本的にメモリ上でアイテムをエンティティとして扱うために使われるID、「今のところ」保存しなくてよい
        /// </summary>
        ItemInstanceId ItemInstanceId { get; }
        
        ItemProcessResult AddItem(IItemStack receiveItemStack);
        IItemStack SubItem(int subCount);
        
        /// <summary>
        ///     アイテムを追加できるときtrueを返す
        ///     あまりがある時はfalceを返す
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        bool IsAllowedToAdd(IItemStack item);
        
        /// <summary>
        ///     アイテムを追加できるが、あまりが発生する場合trueを返します
        ///     あまりが発生しない場合でも追加ができるならtrueを返します
        ///     IDが違うことで追加ができない場合はfalseを返します
        /// </summary>
        /// <returns>あまりが出ても追加できるときはtrue</returns>
        bool IsAllowedToAddWithRemain(IItemStack item);
    }
}