using Core.Master;

namespace Game.Block.Interface.Component
{
    // 打ち上げ口が次の便から積む積荷の選択。未選択と選択済みを型で分け、未選択を値の欠落で表さない
    // The cargo a launch site loads from its next flight; unselected and selected are separate types, never an absent value
    public abstract class OrbitalCargoSelection
    {
        public static readonly UnselectedOrbitalCargo Unselected = new();

        // 派生をこのアセンブリの2型に閉じる
        // Close derivation to the two types in this assembly
        private protected OrbitalCargoSelection() { }

        // 選んだ種類か。未選択はどの種類とも一致しない。受け入れ・入口・返却の判定はすべてここを呼ぶ
        // Whether the kind is the selected one; unselected matches none, and every accept, inlet and refund check calls this
        public abstract bool IsSelected(ItemId payloadItemId);

        // 状態詳細の積荷の欄へ載せる形。未選択は null（通信の型は欄＋null のまま）
        // The form for the state detail cargo field; null while unselected (the wire type keeps its nullable field)
        public abstract ItemId? ToNullablePayloadItemId();

        // セーブとブループリントへ書くアイテム GUID の文字列。未選択は null
        // The item GUID text written to saves and blueprints; null while unselected
        public abstract string ToItemGuidTextOrNull();
    }

    public sealed class UnselectedOrbitalCargo : OrbitalCargoSelection
    {
        internal UnselectedOrbitalCargo() { }

        public override bool IsSelected(ItemId payloadItemId) => false;
        public override ItemId? ToNullablePayloadItemId() => null;
        public override string ToItemGuidTextOrNull() => null;
    }

    public sealed class SelectedOrbitalCargo : OrbitalCargoSelection
    {
        public readonly ItemId PayloadItemId;

        public SelectedOrbitalCargo(ItemId payloadItemId)
        {
            PayloadItemId = payloadItemId;
        }

        public override bool IsSelected(ItemId payloadItemId) => PayloadItemId == payloadItemId;
        public override ItemId? ToNullablePayloadItemId() => PayloadItemId;
        public override string ToItemGuidTextOrNull() => MasterHolder.ItemMaster.GetItemGuid(PayloadItemId).ToString();
    }
}
