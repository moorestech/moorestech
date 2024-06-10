using MessagePack;

namespace Core.Item.Interface
{
    [MessagePackObject]
    public abstract class ItemStackMetaData
    {
        public abstract bool Equals(ItemStackMetaData target);
    }
}