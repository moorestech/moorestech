using Game.Common.MessagePack;

namespace Game.PlayerInventory.Interface.Subscription
{
    // サブスクリプション識別子の共通インターフェース
    // Common interface for subscription identifiers
    public interface ISubInventoryIdentifier
    {
        InventoryType Type { get; }

        // HashSetで使用するので、EqualsとGetHashCodeをオーバーライドする必要がある
        // Since it is used in HashSet, Equals and GetHashCode need to be overridden
        bool Equals(object obj);
        int GetHashCode();
    }
}
