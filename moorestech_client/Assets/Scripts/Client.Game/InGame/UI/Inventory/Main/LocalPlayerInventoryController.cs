using System;
using System.Collections.Generic;
using System.Linq;
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.Inventory.Equipment;
using Client.Game.InGame.UI.Inventory.Main.Operations;
using Core.Item.Interface;
using Core.Master;
using Game.Context;
using Game.PlayerInventory.Interface;
using Game.PlayerInventory.Interface.Subscription;
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

        // grab の書き換えだけを知らせる。OnInventoryRefreshed の既存の購読者（研究の取り直し等）に要求を増やさないため別の流れにする
        // Notifies only grab rewrites; kept separate so existing OnInventoryRefreshed subscribers (research refetch, etc.) get no extra requests
        public IObservable<Unit> OnGrabChanged => _onGrabChanged;
        private readonly Subject<Unit> _onGrabChanged = new();

        private readonly LocalPlayerInventory _localPlayerInventory;
        private readonly LocalPlayerEquipment _localPlayerEquipment;
        private ISubInventory _subInventory;

        public LocalPlayerInventoryController(ILocalPlayerInventory localPlayerInventoryMainAndSubCombine, LocalPlayerEquipment localPlayerEquipment)
        {
            _localPlayerInventory = (LocalPlayerInventory)localPlayerInventoryMainAndSubCombine;
            _localPlayerEquipment = localPlayerEquipment;
            GrabInventory = ServerContext.ItemStackFactory.Create(new ItemId(0), 0);
        }

        // ローカル座標系（結合スロット / grab / 装備スロット）から現在のアイテムを読む
        // Reads the current stack for a local coordinate (combined slot / grab / equipment slot)
        public IItemStack GetItem(LocalMoveInventoryType type, int slot)
        {
            return type switch
            {
                LocalMoveInventoryType.MainOrSub => LocalPlayerInventory[slot],
                LocalMoveInventoryType.Grab => GrabInventory,
                LocalMoveInventoryType.Equipment => _localPlayerEquipment.Slots[slot],
                _ => throw new ArgumentOutOfRangeException(nameof(type), type, null),
            };
        }

        public LocalItemMoveResult MoveItem(LocalMoveInventoryType from, int fromSlot, LocalMoveInventoryType to, int toSlot, int count)
        {
            return MoveItem(from, fromSlot, to, toSlot, count, true);
        }

        public LocalItemMoveResult MoveItem(LocalMoveInventoryType from, int fromSlot, LocalMoveInventoryType to, int toSlot, int count, bool isMoveSendData)
        {
            // 移動の可否と何もしない移動の判定はここ 1 か所（Web・UITK・uGUI 共通の口）
            // The only place that judges denial and no-effect moves (shared entry for Web, UITK and uGUI)
            var fromInvItem = GetItem(from, fromSlot);
            var toInvItem = GetItem(to, toSlot);
            var result = JudgeMove(from == to && fromSlot == toSlot, fromInvItem, toInvItem, count);
            if (result != LocalItemMoveResult.Moved) return result;

            SetInventory();

            // grab はインデクサを通らないので、書き換えたら掴みの流れで知らせる
            // Grab bypasses the indexer, so announce rewrites on the grab stream
            if (from == LocalMoveInventoryType.Grab || to == LocalMoveInventoryType.Grab) _onGrabChanged.OnNext(Unit.Default);

            // サーバー送信は結合スロット→サーバースロット変換を担う専用クラスへ委譲する
            // Delegate server dispatch (combined-slot to server-slot conversion) to a dedicated class
            if (isMoveSendData) InventoryMoveServerDispatcher.SendMoveItemData(_subInventory, _localPlayerInventory.MainSlotCount, from, fromSlot, to, toSlot, count);
            return LocalItemMoveResult.Moved;

            #region Internal

            void SetInventory()
            {
                var itemStackFactory = ServerContext.ItemStackFactory;

                // 別IDのスロットへ全量移動したときはサーバーのSwapSlotと同じく入れ替える（部分移動はMoveItem本体で弾き済み）
                // A full move onto a different-id slot swaps, matching the server's SwapSlot; a partial move is already rejected in MoveItem
                if (toInvItem.Id != ItemMaster.EmptyItemId && toInvItem.Id != fromInvItem.Id)
                {
                    SetItem(to, toSlot, fromInvItem);
                    SetItem(from, fromSlot, toInvItem);
                    return;
                }

                var moveItem = itemStackFactory.Create(fromInvItem.Id, count);

                var add = toInvItem.AddItem(moveItem);
                SetItem(to, toSlot, add.ProcessResultItemStack);

                var fromItemCount = fromInvItem.Count - count + add.RemainderItemStack.Count;
                SetItem(from, fromSlot, itemStackFactory.Create(fromInvItem.Id, fromItemCount));
            }

            // 装備もサーバー応答を待たずローカルへ先に書く。サーバーのスロット更新イベントが後から正へ揃える
            // Equipment is also written locally before any server response; the server's slot update event reconciles it later
            void SetItem(LocalMoveInventoryType type, int slot, IItemStack itemStack)
            {
                switch (type)
                {
                    case LocalMoveInventoryType.MainOrSub:
                        _localPlayerInventory[slot] = itemStack;
                        break;
                    case LocalMoveInventoryType.Grab:
                        GrabInventory = itemStack;
                        break;
                    case LocalMoveInventoryType.Equipment:
                        _localPlayerEquipment.ApplySlotUpdate(slot, itemStack);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(type), type, null);
                }
            }
            #endregion
        }

        private static LocalItemMoveResult JudgeMove(bool isSameSlot, IItemStack fromItem, IItemStack toItem, int count)
        {
            // 同一スロットは加算の後の書き戻しでスタックが消えるので何もしない（Web では成功）
            // A same-slot move would lose the stack in the add-then-write-back, so it does nothing (a success on Web)
            if (isSameSlot) return LocalItemMoveResult.NoEffect;
            if (fromItem.Id == ItemMaster.EmptyItemId) return LocalItemMoveResult.SourceEmpty;
            if (fromItem.Count < count) return LocalItemMoveResult.SourceCountShort;

            // 0 個以下は swap 分岐で行先を空へ吸い出し、別 ID への部分移動は確定 no-op なので送らない
            // count<=0 would drain the target via the swap branch and a partial move onto another id is a sure no-op, so neither is sent
            if (count <= 0) return LocalItemMoveResult.NoEffect;
            var isDifferentId = toItem.Id != ItemMaster.EmptyItemId && toItem.Id != fromItem.Id;
            return isDifferentId && count != fromItem.Count ? LocalItemMoveResult.NoEffect : LocalItemMoveResult.Moved;
        }

        public void CollectItems(LocalMoveInventoryType targetType, int targetSlot)
        {
            InventoryItemCollector.Collect(this, targetType, targetSlot);
        }

        public void SortInventory()
        {
            // メインインベントリを整理（ホットバー除外はサーバー側で実施）
            // Sort the main inventory (hotbar exclusion is handled on the server).
            ClientContext.VanillaApi.SendOnly.SortInventory(CreateMainMessage());

            // 開いているサブインベントリがあれば整理する
            // Also sort the currently open sub-inventory, if any.
            if (_subInventory != null && _subInventory.IsEnableSubInventory())
                ClientContext.VanillaApi.SendOnly.SortInventory(_subInventory.ISubInventoryIdentifier.ToMessagePack());
        }

        public void SetGrabItem(IItemStack itemStack)
        {
            GrabInventory = itemStack;
            _onInventoryRefreshed.OnNext(Unit.Default);
            _onGrabChanged.OnNext(Unit.Default);
        }
        
        public void SetMainItem(int slot, IItemStack itemStack)
        {
            // 範囲外スロットの通知はレベルアップによる拡張なので末尾まで成長させる
            // An out-of-range slot notification means a level-up expansion, so grow to that slot
            if (_localPlayerInventory.MainSlotCount <= slot) _localPlayerInventory.EnsureMainSlotCount(slot + 1);
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
}
