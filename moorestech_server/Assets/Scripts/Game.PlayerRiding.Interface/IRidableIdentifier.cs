namespace Game.PlayerRiding.Interface
{
    // 乗り物を指す識別子の共通インターフェース。ISubInventoryIdentifier に倣う。
    // Common interface for identifiers that point at a ridable. Mirrors ISubInventoryIdentifier.
    public interface IRidableIdentifier
    {
        RidableType Type { get; }

        // Dictionary / HashSet のキーに使うので Equals と GetHashCode をオーバーライドする
        // Used as Dictionary / HashSet keys, so Equals and GetHashCode must be overridden.
        bool Equals(object obj);
        int GetHashCode();
    }
}
