using System;
using System.Collections.Generic;
using Client.Game.InGame.Context;
using Core.Item.Interface;
using Core.Master;
using Game.Context;
using Game.PlayerInventory.Interface;
using Game.PlayerInventory.Interface.Subscription;
using Server.Protocol.PacketResponse.Util.InventoryMoveUtil;
using Server.Util.MessagePack;
using static Server.Util.MessagePack.InventoryIdentifierMessagePack;

namespace Client.Game.InGame.UI.Inventory.Main
{
    public class LocalPlayerInventoryController
    {
        public ILocalPlayerInventory LocalPlayerInventory => _localPlayerInventory;
        public IItemStack GrabInventory { get; private set; }
        
        private readonly LocalPlayerInventory _localPlayerInventory;
        private ISubInventory _subInventory;
        
        public LocalPlayerInventoryController(ILocalPlayerInventory localPlayerInventoryMainAndSubCombine)
        {
            _localPlayerInventory = (LocalPlayerInventory)localPlayerInventoryMainAndSubCombine;
            GrabInventory = ServerContext.ItemStackFactory.Create(new ItemId(0), 0);
        }
        
        public void MoveItem(LocalMoveInventoryType from, int fromSlot, LocalMoveInventoryType to, int toSlot, int count, bool isMoveSendData = true)
        {
            var fromInvItem = from switch
            {
                LocalMoveInventoryType.MainOrSub => LocalPlayerInventory[fromSlot],
                LocalMoveInventoryType.Grab => GrabInventory,
                _ => throw new ArgumentOutOfRangeException(nameof(from), from, null),
            };
            
            if (fromInvItem.Count < count) return;
            
            SetInventory();
            
            if (isMoveSendData) SendMoveItemData();
            
            #region Internal
            
            void SetInventory()
            {
                var itemStackFactory = ServerContext.ItemStackFactory;
                
                var toInvItem = to switch
                {
                    LocalMoveInventoryType.MainOrSub => LocalPlayerInventory[toSlot],
                    LocalMoveInventoryType.Grab => GrabInventory,
                    _ => throw new ArgumentOutOfRangeException(nameof(to), to, null),
                };
                var moveItem = itemStackFactory.Create(fromInvItem.Id, count);
                
                var add = toInvItem.AddItem(moveItem);
                switch (to)
                {
                    case LocalMoveInventoryType.MainOrSub:
                        _localPlayerInventory[toSlot] = add.ProcessResultItemStack;
                        break;
                    case LocalMoveInventoryType.Grab:
                        GrabInventory = add.ProcessResultItemStack;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(to), to, null);
                }
                
                var fromItemCount = fromInvItem.Count - count + add.RemainderItemStack.Count;
                var fromItem = itemStackFactory.Create(fromInvItem.Id, fromItemCount);
                switch (from)
                {
                    case LocalMoveInventoryType.Grab:
                        GrabInventory = fromItem;
                        break;
                    default:
                        _localPlayerInventory[fromSlot] = fromItem;
                        break;
                }
            }
            
            void SendMoveItemData()
            {
                // ローカル結合スロットをサーバーのインベントリ内スロットへ変換する
                // Convert combined local slots into inventory-local server slots.
                var fromIdentifier = GetServerInventoryIdentifier(from, fromSlot);
                var toIdentifier = GetServerInventoryIdentifier(to, toSlot);
                var fromServerSlot = GetServerInventorySlot(from, fromSlot);
                var toServerSlot = GetServerInventorySlot(to, toSlot);
                ClientContext.VanillaApi.SendOnly.ItemMove(count, ItemMoveType.SwapSlot, fromIdentifier, fromServerSlot, toIdentifier, toServerSlot);
            }
            
            InventoryIdentifierMessagePack GetServerInventoryIdentifier(LocalMoveInventoryType localType, int localSlot)
            {
                return localType switch
                {
                    LocalMoveInventoryType.MainOrSub => localSlot < PlayerInventoryConst.MainInventorySize
                        ? CreateMainMessage(ClientContext.PlayerConnectionSetting.PlayerId)
                        : _subInventory.ISubInventoryIdentifier.ToMessagePack(),
                    LocalMoveInventoryType.Grab => CreateGrabMessage(ClientContext.PlayerConnectionSetting.PlayerId),
                    _ => throw new ArgumentOutOfRangeException(nameof(localType), localType, null),
                };
            }

            int GetServerInventorySlot(LocalMoveInventoryType localType, int localSlot)
            {
                return localType switch
                {
                    LocalMoveInventoryType.MainOrSub => localSlot < PlayerInventoryConst.MainInventorySize
                        ? localSlot
                        : localSlot - PlayerInventoryConst.MainInventorySize,
                    LocalMoveInventoryType.Grab => 0,
                    _ => throw new ArgumentOutOfRangeException(nameof(localType), localType, null),
                };
            }
            
            #endregion
        }
        
        public void SortInventory()
        {
            // メインインベントリを整理（ホットバー除外はサーバー側で実施）
            // Sort the main inventory (hotbar exclusion is handled on the server).
            ClientContext.VanillaApi.SendOnly.SortInventory(CreateMainMessage(ClientContext.PlayerConnectionSetting.PlayerId));

            // 開いているサブインベントリがあれば整理する
            // Also sort the currently open sub-inventory, if any.
            if (_subInventory != null && _subInventory.IsEnableSubInventory())
                ClientContext.VanillaApi.SendOnly.SortInventory(_subInventory.ISubInventoryIdentifier.ToMessagePack());
        }

        public void SetGrabItem(IItemStack itemStack)
        {
            GrabInventory = itemStack;
        }
        
        public void SetMainItem(int slot, IItemStack itemStack)
        {
            _localPlayerInventory[slot] = itemStack;
        }
        
        public void SetSubInventory(ISubInventory subInventory)
        {
            _localPlayerInventory.SetSubInventory(subInventory);
            _subInventory = subInventory;
        }
        
        public void SetMainInventory(List<IItemStack> inventoryMainInventory)
        {
            _localPlayerInventory.SetMainInventory(inventoryMainInventory);
        }
    }
    
    public enum LocalMoveInventoryType
    {
        MainOrSub, //メインインベントリとサブインベントリの両方（ドラッグアンドドロップなどでは統一して扱うから
        Grab, //持ち手のインベントリ
    }
}
