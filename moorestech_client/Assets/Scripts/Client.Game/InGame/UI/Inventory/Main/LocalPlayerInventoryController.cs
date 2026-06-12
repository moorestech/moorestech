using System;
using System.Collections.Generic;
using System.Linq;
using Client.Game.InGame.Context;
using Core.Item.Interface;
using Core.Master;
using Game.Context;
using Game.PlayerInventory.Interface;
using Game.PlayerInventory.Interface.Subscription;
using Server.Protocol.PacketResponse.Util.InventoryMoveUtil;
using Server.Util.MessagePack;
using UniRx;
using static Server.Util.MessagePack.InventoryIdentifierMessagePack;

namespace Client.Game.InGame.UI.Inventory.Main
{
    public class LocalPlayerInventoryController
    {
        public ILocalPlayerInventory LocalPlayerInventory => _localPlayerInventory;
        public IItemStack GrabInventory { get; private set; }

        // grab・全置換などインデクサを経由しない更新の通知
        // Notifies updates that bypass the indexer, such as grab or full replacement
        public IObservable<Unit> OnInventoryRefreshed => _onInventoryRefreshed;
        private readonly Subject<Unit> _onInventoryRefreshed = new();

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
        
        public void CollectItems(LocalMoveInventoryType targetType, int targetSlot)
        {
            // 同種アイテムを所持数の少ない順に集積先へ移す（uGUI ダブルクリックと Web collect の共通実装）
            // Gather same-type stacks smallest-first into the target; shared by uGUI double-click and web collect
            var isGrabTarget = targetType == LocalMoveInventoryType.Grab;
            var collectTarget = isGrabTarget ? GrabInventory : LocalPlayerInventory[targetSlot];
            if (collectTarget.Id == ItemMaster.EmptyItemId) return;

            // 集積先自身は移動元から除外する
            // Exclude the target slot itself from the sources
            var sourceSlots = LocalPlayerInventory
                .Select((item, index) => (item, index))
                .Where(x => x.item.Id == collectTarget.Id)
                .Where(x => isGrabTarget || x.index != targetSlot)
                .OrderBy(x => x.item.Count)
                .Select(x => x.index)
                .ToList();

            foreach (var index in sourceSlots)
            {
                var added = collectTarget.AddItem(LocalPlayerInventory[index]);
                var moveCount = LocalPlayerInventory[index].Count - added.RemainderItemStack.Count;

                // 1個も移せない＝集積先が満杯なので終了
                // Zero movable items means the target is full; stop here
                if (moveCount <= 0) break;
                MoveItem(LocalMoveInventoryType.MainOrSub, index, targetType, targetSlot, moveCount);
                collectTarget = added.ProcessResultItemStack;

                // 余りが出たら集積先が満杯なので終了
                // A remainder means the target stack is full; stop here
                if (added.RemainderItemStack.Count != 0) break;
            }
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
            _onInventoryRefreshed.OnNext(Unit.Default);
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
            _onInventoryRefreshed.OnNext(Unit.Default);
        }
    }
    
    public enum LocalMoveInventoryType
    {
        MainOrSub, //メインインベントリとサブインベントリの両方（ドラッグアンドドロップなどでは統一して扱うから
        Grab, //持ち手のインベントリ
    }
}
