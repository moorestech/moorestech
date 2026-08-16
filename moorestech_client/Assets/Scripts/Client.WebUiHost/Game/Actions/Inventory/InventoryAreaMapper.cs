using Client.Game.InGame.UI.Inventory.Main;
using Core.Master;
using Newtonsoft.Json.Linq;

namespace Client.WebUiHost.Game.Actions
{
    /// <summary>
    /// Web の area/slot 表現をローカルインベントリ座標へ変換する
    /// Maps web-side area/slot pairs to local inventory coordinates
    /// </summary>
    public static class InventoryAreaMapper
    {
        public static bool TryGetLocalSlot(string area, int slot, int mainSlotCount, out LocalMoveInventoryType type, out int localSlot)
        {
            switch (area)
            {
                case "main" when 0 <= slot && slot < mainSlotCount:
                    type = LocalMoveInventoryType.MainOrSub;
                    localSlot = slot;
                    return true;
                // 装備は結合スロットではないため、枠数はマスタ由来の装備枠数で判定する
                // Equipment is not part of the combined slots, so the bound is the master-derived equipment slot count
                case "equipment" when 0 <= slot && slot < MasterHolder.ItemMaster.Items.EquipmentSlotCount:
                    type = LocalMoveInventoryType.Equipment;
                    localSlot = slot;
                    return true;
                case "grab":
                    type = LocalMoveInventoryType.Grab;
                    localSlot = 0;
                    return true;
                default:
                    type = LocalMoveInventoryType.MainOrSub;
                    localSlot = -1;
                    return false;
            }
        }

        // クリック可能スロット（main/equipment）のみ許可。grab は collect 入力として不正
        // Accept only clickable slots (main/equipment); grab is invalid as a collect input
        public static bool TryParseClickableSlotRef(JToken token, int mainSlotCount, out LocalMoveInventoryType type, out int localSlot)
        {
            if (!TryParseSlotRef(token, mainSlotCount, out type, out localSlot)) return false;
            if (type != LocalMoveInventoryType.Grab) return true;

            type = LocalMoveInventoryType.MainOrSub;
            localSlot = -1;
            return false;
        }

        // area/slot 形式の JToken を変換
        // Parse an area/slot-shaped JToken
        public static bool TryParseSlotRef(JToken token, int mainSlotCount, out LocalMoveInventoryType type, out int localSlot)
        {
            type = LocalMoveInventoryType.MainOrSub;
            localSlot = -1;
            if (token is not JObject obj) return false;

            // 外部入力のため型を検証し例外を出さず false を返す
            // Validate external input types and return false instead of throwing
            if (obj["area"] is not JValue { Type: JTokenType.String } areaValue) return false;

            // slot 欠落は grab のみ許可
            // Missing slot is allowed only for grab
            var area = (string)areaValue;
            var slotToken = obj["slot"];
            var slot = 0;
            if (slotToken == null)
            {
                if (area != "grab") return false;
            }
            else
            {
                // int 範囲の整数のみ許可（範囲外 long / BigInteger は拒否）
                // Only int-range integers allowed, out-of-range long / BigInteger rejected
                if (slotToken is not JValue { Value: long slotLong }) return false;
                if (slotLong < int.MinValue || int.MaxValue < slotLong) return false;
                slot = (int)slotLong;
            }

            return TryGetLocalSlot(area, slot, mainSlotCount, out type, out localSlot);
        }
    }
}
