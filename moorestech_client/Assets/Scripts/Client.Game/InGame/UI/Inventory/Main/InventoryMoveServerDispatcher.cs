// [uGUI廃止Phase1] Web UI移行済みのため未メンテ・描画恒久停止。Phase2で削除予定（docs/webui/ugui-retirement-plan.md）
// [uGUI retirement Phase1] Unmaintained; rendering permanently disabled after the Web UI migration. Slated for deletion in Phase2 (docs/webui/ugui-retirement-plan.md)
using System;
using Client.Game.InGame.Context;
using Game.PlayerInventory.Interface;
using Game.PlayerInventory.Interface.Subscription;
using Server.Protocol.PacketResponse.Util.InventoryMoveUtil;
using Server.Util.MessagePack;
using static Server.Util.MessagePack.InventoryIdentifierMessagePack;

namespace Client.Game.InGame.UI.Inventory.Main
{
    /// <summary>
    /// ローカル結合スロットをサーバーのインベントリ内スロットへ変換して移動を送信する
    /// Converts combined local slots into inventory-local server slots and sends the move
    /// </summary>
    public static class InventoryMoveServerDispatcher
    {
        public static void SendMoveItemData(ISubInventory subInventory, int mainSlotCount, LocalMoveInventoryType from, int fromSlot, LocalMoveInventoryType to, int toSlot, int count)
        {
            // 結合スロットをサーバーの識別子とスロットへ変換して送信する
            // Convert combined slots into server identifiers/slots, then send
            var fromIdentifier = GetServerInventoryIdentifier(from, fromSlot);
            var toIdentifier = GetServerInventoryIdentifier(to, toSlot);
            var fromServerSlot = GetServerInventorySlot(from, fromSlot);
            var toServerSlot = GetServerInventorySlot(to, toSlot);
            ClientContext.VanillaApi.SendOnly.ItemMove(count, ItemMoveType.SwapSlot, fromIdentifier, fromServerSlot, toIdentifier, toServerSlot);

            #region Internal

            InventoryIdentifierMessagePack GetServerInventoryIdentifier(LocalMoveInventoryType localType, int localSlot)
            {
                return localType switch
                {
                    LocalMoveInventoryType.MainOrSub => localSlot < mainSlotCount
                        ? CreateMainMessage(ClientContext.PlayerConnectionSetting.PlayerId)
                        : subInventory.ISubInventoryIdentifier.ToMessagePack(),
                    LocalMoveInventoryType.Grab => CreateGrabMessage(ClientContext.PlayerConnectionSetting.PlayerId),
                    _ => throw new ArgumentOutOfRangeException(nameof(localType), localType, null),
                };
            }

            int GetServerInventorySlot(LocalMoveInventoryType localType, int localSlot)
            {
                return localType switch
                {
                    LocalMoveInventoryType.MainOrSub => localSlot < mainSlotCount
                        ? localSlot
                        : localSlot - mainSlotCount,
                    LocalMoveInventoryType.Grab => 0,
                    _ => throw new ArgumentOutOfRangeException(nameof(localType), localType, null),
                };
            }

            #endregion
        }
    }
}
