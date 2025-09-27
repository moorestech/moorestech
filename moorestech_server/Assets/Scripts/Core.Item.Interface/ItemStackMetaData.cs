using MessagePack;

namespace Core.Item.Interface
{
    /// <summary>
    /// 概念だけ存在する
    /// 実際に使われているところはない
    /// </summary>
    //[MessagePackObject]
    public abstract class ItemStackMetaData
    {
        public abstract bool Equals(ItemStackMetaData target);
    }
}