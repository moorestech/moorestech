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
            var playerId = ClientContext.PlayerConnectionSetting.PlayerId;
            var fromCoordinate = ToServerCoordinate(subInventory, mainSlotCount, playerId, from, fromSlot);
            var toCoordinate = ToServerCoordinate(subInventory, mainSlotCount, playerId, to, toSlot);
            ClientContext.VanillaApi.SendOnly.ItemMove(count, ItemMoveType.SwapSlot, fromCoordinate.identifier, fromCoordinate.serverSlot, toCoordinate.identifier, toCoordinate.serverSlot);
        }

        /// <summary>
        ///     ローカル座標（結合スロット / grab / 装備スロット）をサーバーの識別子とスロットへ変換する純粋関数
        ///     Pure conversion from a local coordinate (combined slot / grab / equipment slot) to a server identifier and slot
        /// </summary>
        public static (InventoryIdentifierMessagePack identifier, int serverSlot) ToServerCoordinate(ISubInventory subInventory, int mainSlotCount, int playerId, LocalMoveInventoryType localType, int localSlot)
        {
            switch (localType)
            {
                case LocalMoveInventoryType.MainOrSub:
                    // 結合スロットは mainSlotCount を境にメインとサブへ割り振る
                    // The combined slot splits into main and sub at the mainSlotCount boundary
                    return localSlot < mainSlotCount
                        ? (CreateMainMessage(playerId), localSlot)
                        : (subInventory.ISubInventoryIdentifier.ToMessagePack(), localSlot - mainSlotCount);
                case LocalMoveInventoryType.Grab:
                    return (CreateGrabMessage(playerId), 0);
                case LocalMoveInventoryType.Equipment:
                    // 装備は結合スロットではないため、ローカルスロットがそのままサーバースロットになる
                    // Equipment is not a combined slot, so the local slot is the server slot as-is
                    return (CreateEquipmentMessage(playerId), localSlot);
                default:
                    throw new ArgumentOutOfRangeException(nameof(localType), localType, null);
            }
        }
    }
}
