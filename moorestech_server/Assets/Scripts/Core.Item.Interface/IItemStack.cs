using Core.Master;

namespace Core.Item.Interface
{
    public interface IItemStack
    {
        ItemId Id { get; }
        int Count { get; }
        
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
        ///     アイテムを追加できるが、余りが発生する場合trueを返します
        ///     あまりが発生しない場合でも追加ができるならtrueを返します
        ///     IDが違うことで追加ができない場合はfalseを返します
        /// </summary>
        /// <returns>あまりが出ても追加できるときはtrue</returns>
        bool IsAllowedToAddWithRemain(IItemStack item);
        
        // ひとまず追加はしたが、用途がないので放置。 moddingとかに使うかもしれないので取っておいてはいるが、動作はサポートしていないです。
        // I added it for the time being, but left it alone because I have no use for it. I'm keeping it because I might use it for modding or something, but I don't support its operation.
        public ItemStackMetaData GetMeta(string key);
        public bool TryGetMeta(string key, out ItemStackMetaData value); 
        public IItemStack SetMeta(string key, ItemStackMetaData value);
    }
}