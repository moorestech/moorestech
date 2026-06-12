using Client.Game.InGame.UI.Inventory.Main;
using Game.PlayerInventory.Interface;
using Newtonsoft.Json.Linq;

namespace Client.WebUiHost.Game.Actions
{
    /// <summary>
    /// Web の area/slot 表現をローカルインベントリ座標へ変換する
    /// Maps web-side area/slot pairs to local inventory coordinates
    /// </summary>
    public static class InventoryAreaMapper
    {
        // メイン部 = ホットバー行を除いた 36 スロット
        // Main section = 36 slots excluding the hotbar row
        public static readonly int MainAreaSize = PlayerInventoryConst.MainInventorySize - PlayerInventoryConst.MainInventoryColumns;

        public static bool TryGetLocalSlot(string area, int slot, out LocalMoveInventoryType type, out int localSlot)
        {
            switch (area)
            {
                case "main" when 0 <= slot && slot < MainAreaSize:
                    type = LocalMoveInventoryType.MainOrSub;
                    localSlot = slot;
                    return true;
                case "hotbar" when 0 <= slot && slot < PlayerInventoryConst.MainInventoryColumns:
                    type = LocalMoveInventoryType.MainOrSub;
                    localSlot = MainAreaSize + slot;
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

        // payload 中の {"area":"main","slot":3} 形式の JToken を変換する
        // Parse a {"area":"main","slot":3}-shaped JToken from a payload
        public static bool TryParseSlotRef(JToken token, out LocalMoveInventoryType type, out int localSlot)
        {
            type = LocalMoveInventoryType.MainOrSub;
            localSlot = -1;
            if (token is not JObject obj) return false;

            // 外部入力のため型を検証し例外を出さず false を返す
            // Validate external input types and return false instead of throwing
            if (obj["area"] is not JValue { Type: JTokenType.String } areaValue) return false;

            // slot 欠落は grab のみ許可、それ以外の area ではスロット指定必須
            // Missing slot is allowed only for grab; other areas require an explicit slot
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
                if (slotLong < int.MinValue || slotLong > int.MaxValue) return false;
                slot = (int)slotLong;
            }

            return TryGetLocalSlot(area, slot, out type, out localSlot);
        }
    }
}
