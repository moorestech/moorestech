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
            var area = obj.Value<string>("area");
            var slot = obj.Value<int?>("slot") ?? 0;
            return TryGetLocalSlot(area, slot, out type, out localSlot);
        }
    }
}
