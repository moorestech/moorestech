using System;
using Core.Master;

namespace Game.Block.Blocks.OrbitalCargo
{
    // 積荷の GUID 文字列を今のマスタの ItemId へ戻す。セーブの読込とブループリントの適用が同じ範囲の GUID を受け入れるよう1か所にする
    // Resolve a cargo GUID text to an ItemId in the current master; kept in one place so save loading and blueprint application accept the same GUIDs
    internal static class OrbitalCargoItemGuid
    {
        // 読めない文字列と今のマスタに無い GUID はどちらも false。理由のログは呼び手が出す
        // Both unparsable text and a GUID missing from the current master return false; callers log the reason
        public static bool TryParseExistingItemGuid(string guidText, out ItemId itemId)
        {
            itemId = ItemMaster.EmptyItemId;
            if (!Guid.TryParse(guidText, out var guid) || !MasterHolder.ItemMaster.ExistItemId(guid)) return false;
            itemId = MasterHolder.ItemMaster.GetItemId(guid);
            return true;
        }
    }
}
