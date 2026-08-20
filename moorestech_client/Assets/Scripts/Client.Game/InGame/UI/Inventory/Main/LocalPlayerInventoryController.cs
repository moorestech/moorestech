// [uGUI廃止Phase1] uGUI描画は恒久停止・ビューは未メンテ。ただし本クラスは外部（Web UIブリッジ等）から参照中のため削除前に整理が必要（docs/webui/ugui-retirement-plan.md）
// [uGUI retirement Phase1] uGUI rendering is permanently disabled and the view is unmaintained, but this class is still referenced externally (e.g. Web UI bridge); untangle before deletion (docs/webui/ugui-retirement-plan.md)
using System;
using System.Collections.Generic;
using System.Linq;
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.Inventory.Equipment;
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

        public void MoveItem(LocalMoveInventoryType from, int fromSlot, LocalMoveInventoryType to, int toSlot, int count)
        {
            MoveItem(from, fromSlot, to, toSlot, count, true);
        }

        public void MoveItem(LocalMoveInventoryType from, int fromSlot, LocalMoveInventoryType to, int toSlot, int count, bool isMoveSendData)
        {
            var fromInvItem = GetItem(from, fromSlot);

            if (fromInvItem.Count < count) return;

            SetInventory();

            // サーバー送信は結合スロット→サーバースロット変換を担う専用クラスへ委譲する
            // Delegate server dispatch (combined-slot to server-slot conversion) to a dedicated class
            if (isMoveSendData) InventoryMoveServerDispatcher.SendMoveItemData(_subInventory, _localPlayerInventory.MainSlotCount, from, fromSlot, to, toSlot, count);

            #region Internal

            void SetInventory()
            {
                var itemStackFactory = ServerContext.ItemStackFactory;

                var toInvItem = GetItem(to, toSlot);

                // 別IDのスロットへ全量移動したときはサーバーのSwapSlotと同じく入れ替える（部分移動はサーバー側もno-op）
                // A full move onto a different-id slot swaps, matching the server's SwapSlot; a partial move is a no-op there too
                if (toInvItem.Id != ItemMaster.EmptyItemId && toInvItem.Id != fromInvItem.Id)
                {
                    if (count != fromInvItem.Count) return;
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

        public bool TryMoveItem(LocalMoveInventoryType fromType, int fromSlot, LocalMoveInventoryType toType, int toSlot, int count, out string denyReason)
        {
            // 移動元の実在・数量を検証してから MoveItem を呼ぶ（Web/uGUI 共通の検証口）
            // Validate the source stack's presence and count before calling MoveItem (shared web/uGUI guard)
            var fromItem = GetItem(fromType, fromSlot);
            if (fromItem.Id == ItemMaster.EmptyItemId)
            {
                denyReason = "empty_slot";
                return false;
            }
            if (fromItem.Count < count)
            {
                denyReason = "insufficient_count";
                return false;
            }

            denyReason = null;
            MoveItem(fromType, fromSlot, toType, toSlot, count);
            return true;
        }

        public void CollectItems(LocalMoveInventoryType targetType, int targetSlot)
        {
            // 同種アイテムを所持数の少ない順に集積先へ移す（uGUI ダブルクリックと Web collect の共通実装）
            // Gather same-type stacks smallest-first into the target; shared by uGUI double-click and web collect
            var collectTarget = GetItem(targetType, targetSlot);
            if (collectTarget.Id == ItemMaster.EmptyItemId) return;

            // 集積先が結合スロットのときだけ、同じ index を移動元から除外する
            // Exclude the same index from the sources only when the target is a combined slot
            var isCombinedTarget = targetType == LocalMoveInventoryType.MainOrSub;
            var sourceSlots = LocalPlayerInventory
                .Select((item, index) => (item, index))
                .Where(x => x.item.Id == collectTarget.Id)
                .Where(x => !isCombinedTarget || x.index != targetSlot)
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
    
    public enum LocalMoveInventoryType
    {
        MainOrSub, //メインインベントリとサブインベントリの両方（ドラッグアンドドロップなどでは統一して扱うから
        Grab, //持ち手のインベントリ
        Equipment, //装備インベントリ（slotはそのまま装備スロット番号）
    }
}
