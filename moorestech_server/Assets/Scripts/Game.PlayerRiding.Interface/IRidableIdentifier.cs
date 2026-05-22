namespace Game.PlayerRiding.Interface
{
    // 乗り物を指す識別子の共通インターフェース。ISubInventoryIdentifier に倣う。
    // Common interface for identifiers that point at a ridable. Mirrors ISubInventoryIdentifier.
    public interface IRidableIdentifier
    {
        RidableType Type { get; }

        // セーブ用に自身を文字列へ直列化する。型ごとに固有のペイロード形式を持つ（DTO は Type と本文字列のみ保持）。
        // Serializes this identifier into a save-payload string. Each type owns its own payload format.
        string GetSaveState();

        // Dictionary / HashSet のキーに使うので Equals と GetHashCode をオーバーライドする
        // Used as Dictionary / HashSet keys, so Equals and GetHashCode must be overridden.
        bool Equals(object obj);
        int GetHashCode();
    }
}
