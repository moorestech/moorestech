using Core.Item.Interface;

namespace Core.Inventory
{
    /// <summary>
    ///     スロットへの配置可否を宣言する能力インターフェース。SetItemは常に書き込みを実行するため、
    ///     移動/挿入サービスはこれを実装するインベントリへ書き込み前に問い合わせ、不可なら1バイトも書かない
    ///     Capability interface declaring whether a slot accepts a stack. SetItem always writes,
    ///     so move/insert services query an inventory implementing this before writing and write nothing on rejection
    /// </summary>
    public interface ISlotPlacementRestriction
    {
        bool IsAllowedToPlace(int slot, IItemStack itemStack);
    }
}
