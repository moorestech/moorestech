using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Core.Inventory;
using Core.Item.Interface;
using Core.Master;
using Game.Block.Interface.Component;
using Game.Context;
using Game.PlayerInventory.Interface;
using Game.Train.Event;
using Game.Train.Unit;
using Game.Train.Unit.Containers;
using Game.World.Interface.DataStore;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using Server.Protocol.PacketResponse.Util.InventoryMoveUtil;
using Server.Protocol.PacketResponse.Util.InventoryService;
using Server.Util.MessagePack;
using UnityEngine;

namespace Server.Protocol.PacketResponse
{
    /// <summary>
    ///     インベントリでマウスを使ってアイテムの移動を操作するプロトコルです
    /// </summary>
    public class InventoryItemMoveProtocol : IPacketResponse
    {
        public const string ProtocolTag = "va:invItemMove";

        private readonly IPlayerInventoryDataStore _playerInventoryDataStore;
        private readonly ITrainUnitLookupDatastore _trainUnitLookupDatastore;
        private readonly TrainUpdateEvent _trainUpdateEvent;

        public InventoryItemMoveProtocol(ServiceProvider serviceProvider)
        {
            _playerInventoryDataStore = serviceProvider.GetService<IPlayerInventoryDataStore>();
            _trainUnitLookupDatastore = serviceProvider.GetService<ITrainUnitLookupDatastore>();
            _trainUpdateEvent = (TrainUpdateEvent)serviceProvider.GetService<ITrainUpdateEvent>();
        }

        public ProtocolMessagePackBase GetResponse(byte[] payload)
        {
            var data = MessagePackSerializer.Deserialize<InventoryItemMoveProtocolMessagePack>(payload);

            var fromInventory = GetInventory(data.FromInventory.InventoryType, data.PlayerId, data.FromInventory.InventoryIdentifier);
            if (fromInventory == null) return null;

            var fromSlot = data.FromInventory.Slot;
            if (data.FromInventory.InventoryType == ItemMoveInventoryType.SubInventory)
                fromSlot -= PlayerInventoryConst.MainInventorySize;


            var toInventory = GetInventory(data.ToInventory.InventoryType, data.PlayerId, data.ToInventory.InventoryIdentifier);
            if (toInventory == null) return null;

            var toSlot = data.ToInventory.Slot;
            if (data.ToInventory.InventoryType == ItemMoveInventoryType.SubInventory)
                toSlot -= PlayerInventoryConst.MainInventorySize;


            switch (data.ItemMoveType)
            {
                case ItemMoveType.SwapSlot:
                    InventoryItemMoveService.Move(fromInventory, fromSlot, toInventory, toSlot, data.Count);
                    break;
                case ItemMoveType.InsertSlot:
                    InventoryItemInsertService.Insert(fromInventory, fromSlot, toInventory, data.Count);
                    break;
            }

            return null;
        }

        private IOpenableInventory GetInventory(ItemMoveInventoryType inventoryType, int playerId, InventoryIdentifierMessagePack inventoryIdentifier)
        {
            IOpenableInventory inventory = null;
            switch (inventoryType)
            {
                case ItemMoveInventoryType.MainInventory:
                    inventory = _playerInventoryDataStore.GetInventoryData(playerId).MainOpenableInventory;
                    break;
                case ItemMoveInventoryType.GrabInventory:
                    inventory = _playerInventoryDataStore.GetInventoryData(playerId).GrabInventory;
                    break;
                case ItemMoveInventoryType.SubInventory:
                    // ブロック/列車インベントリの場合はInventoryIdentifierから情報を取得
                    // Get information from InventoryIdentifier for block/train inventory
                    if (inventoryIdentifier == null) return null;

                    // InventoryIdentifierのタイプに応じて処理を分岐
                    // Branch processing according to InventoryIdentifier type
                    switch (inventoryIdentifier.InventoryType)
                    {
                        case Server.Util.MessagePack.InventoryType.Block:
                            var pos = inventoryIdentifier.BlockPosition.Vector3Int;
                            inventory = ServerContext.WorldBlockDatastore.ExistsComponent<IOpenableBlockInventoryComponent>(pos)
                                ? ServerContext.WorldBlockDatastore.GetBlock<IOpenableBlockInventoryComponent>(pos)
                                : null;
                            break;
                        case Server.Util.MessagePack.InventoryType.Train:
                            // 列車カーのアイテムコンテナを操作可能なインベントリに変換
                            // Adapt the target train car item container to an openable inventory
                            var trainCarInstanceId = new TrainCarInstanceId(long.Parse(inventoryIdentifier.TrainCarInstanceId));
                            if (_trainUnitLookupDatastore.TryGetTrainCar(trainCarInstanceId, out var trainCar) &&
                                trainCar.Container is ItemTrainCarContainer itemContainer)
                            {
                                inventory = new TrainCarOpenableInventory(trainCarInstanceId, itemContainer, _trainUpdateEvent);
                            }
                            break;
                    }
                    break;
            }

            return inventory;
        }

        private sealed class TrainCarOpenableInventory : IOpenableInventory
        {
            public IReadOnlyList<IItemStack> InventoryItems => _container.InventoryItems.Select(slot => slot.Stack).ToArray();

            private readonly TrainCarInstanceId _trainCarInstanceId;
            private readonly ItemTrainCarContainer _container;
            private readonly TrainUpdateEvent _trainUpdateEvent;

            public TrainCarOpenableInventory(TrainCarInstanceId trainCarInstanceId, ItemTrainCarContainer container, TrainUpdateEvent trainUpdateEvent)
            {
                _trainCarInstanceId = trainCarInstanceId;
                _container = container;
                _trainUpdateEvent = trainUpdateEvent;
            }

            public IItemStack GetItem(int slot)
            {
                return _container.InventoryItems[slot].Stack;
            }

            public void SetItem(int slot, IItemStack itemStack)
            {
                if (GetItem(slot).Equals(itemStack)) return;

                // 列車コンテナ本体を更新して購読中クライアントへ通知
                // Update the train container source and notify subscribed clients
                _container.SetItem(slot, itemStack);
                _trainUpdateEvent.InvokeInventoryUpdate(new TrainInventoryUpdateEventProperties(_trainCarInstanceId, slot, itemStack));
            }

            public void SetItem(int slot, ItemId itemId, int count)
            {
                SetItem(slot, ServerContext.ItemStackFactory.Create(itemId, count));
            }

            public IItemStack ReplaceItem(int slot, IItemStack itemStack)
            {
                var currentItem = GetItem(slot);
                if (currentItem.Id == itemStack.Id)
                {
                    // 同一アイテムは既存スタックに加算して余りを返す
                    // Add matching items to the existing stack and return the remainder
                    var result = currentItem.AddItem(itemStack);
                    SetItem(slot, result.ProcessResultItemStack);
                    return result.RemainderItemStack;
                }

                // 別アイテムはスロットを入れ替えて元アイテムを返す
                // Swap different items and return the previous slot item
                SetItem(slot, itemStack);
                return currentItem;
            }

            public IItemStack ReplaceItem(int slot, ItemId itemId, int count)
            {
                return ReplaceItem(slot, ServerContext.ItemStackFactory.Create(itemId, count));
            }

            public IItemStack InsertItem(IItemStack itemStack)
            {
                var currentItemStack = InsertItemToExistingStacks(itemStack);
                return InsertItemToEmptySlots(currentItemStack);
            }

            public IItemStack InsertItem(ItemId itemId, int count)
            {
                return InsertItem(ServerContext.ItemStackFactory.Create(itemId, count));
            }

            public List<IItemStack> InsertItem(List<IItemStack> itemStacks)
            {
                var remains = new List<IItemStack>();
                foreach (var itemStack in itemStacks)
                {
                    var remain = InsertItem(itemStack);
                    if (remain.Id != ItemMaster.EmptyItemId) remains.Add(remain);
                }

                return remains;
            }

            public bool InsertionCheck(List<IItemStack> itemStacks)
            {
                var inventoryCopy = InventoryItems.ToArray();
                foreach (var itemStack in itemStacks)
                {
                    // コピー上で挿入結果を検証し、実コンテナは変更しない
                    // Validate insertion on a copy without mutating the real container
                    var remain = InsertToArray(inventoryCopy, itemStack);
                    if (remain.Id != ItemMaster.EmptyItemId) return false;
                }

                return true;
            }

            public int GetSlotSize()
            {
                return _container.InventoryItems.Length;
            }

            public ReadOnlyCollection<IItemStack> CreateCopiedItems()
            {
                return new ReadOnlyCollection<IItemStack>(InventoryItems.ToList());
            }

            public override int GetHashCode()
            {
                return _trainCarInstanceId.GetHashCode();
            }

            private IItemStack InsertItemToExistingStacks(IItemStack itemStack)
            {
                return InsertToContainer(itemStack, false);
            }

            private IItemStack InsertItemToEmptySlots(IItemStack itemStack)
            {
                return InsertToContainer(itemStack, true);
            }

            private IItemStack InsertToContainer(IItemStack itemStack, bool emptySlotOnly)
            {
                var currentItemStack = itemStack;
                for (var i = 0; i < _container.InventoryItems.Length; i++)
                {
                    if (currentItemStack.Id == ItemMaster.EmptyItemId) return currentItemStack;
                    if (!CanInsertToSlot(GetItem(i), currentItemStack, emptySlotOnly)) continue;

                    // スロットに入る分だけ加算して余りを次スロットへ回す
                    // Add what fits in this slot and carry the remainder forward
                    var result = GetItem(i).AddItem(currentItemStack);
                    SetItem(i, result.ProcessResultItemStack);
                    currentItemStack = result.RemainderItemStack;
                }

                return currentItemStack;
            }

            private static IItemStack InsertToArray(IItemStack[] inventory, IItemStack itemStack)
            {
                var currentItemStack = InsertToArraySlots(inventory, itemStack, false);
                return InsertToArraySlots(inventory, currentItemStack, true);

                #region Internal

                IItemStack InsertToArraySlots(IItemStack[] targetInventory, IItemStack targetItemStack, bool emptySlotOnly)
                {
                    var current = targetItemStack;
                    for (var i = 0; i < targetInventory.Length; i++)
                    {
                        if (current.Id == ItemMaster.EmptyItemId) return current;
                        if (!CanInsertToSlot(targetInventory[i], current, emptySlotOnly)) continue;

                        var result = targetInventory[i].AddItem(current);
                        targetInventory[i] = result.ProcessResultItemStack;
                        current = result.RemainderItemStack;
                    }

                    return current;
                }

                #endregion
            }

            private static bool CanInsertToSlot(IItemStack slotItem, IItemStack insertItem, bool emptySlotOnly)
            {
                if (emptySlotOnly) return slotItem.Id == ItemMaster.EmptyItemId;
                return slotItem.Id != ItemMaster.EmptyItemId && slotItem.Id == insertItem.Id;
            }
        }

        [MessagePackObject]
        public class InventoryItemMoveProtocolMessagePack : ProtocolMessagePackBase
        {
            [Key(2)] public int PlayerId { get; set; }
            [Key(3)] public int Count { get; set; }
            [Key(4)] public int ItemMoveTypeId { get; set; }
            [IgnoreMember] public ItemMoveType ItemMoveType => (ItemMoveType)ItemMoveTypeId;
            [Key(5)] public ItemMoveInventoryInfoMessagePack FromInventory { get; set; }
            [Key(6)] public ItemMoveInventoryInfoMessagePack ToInventory { get; set; }


            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public InventoryItemMoveProtocolMessagePack() { }
            public InventoryItemMoveProtocolMessagePack(int playerId, int count, ItemMoveType itemMoveType,
                ItemMoveInventoryInfo inventory, int fromSlot,
                ItemMoveInventoryInfo toInventory, int toSlot)
            {
                Tag = ProtocolTag;
                PlayerId = playerId;
                Count = count;

                ItemMoveTypeId = (int)itemMoveType;
                FromInventory = new ItemMoveInventoryInfoMessagePack(inventory, fromSlot);
                ToInventory = new ItemMoveInventoryInfoMessagePack(toInventory, toSlot);
            }
        }

        [MessagePackObject]
        public class ItemMoveInventoryInfoMessagePack
        {
            [Obsolete("シリアライズ用の値です。InventoryTypeを使用してください。")]
            [Key(2)] public int InventoryId { get; set; }

            [IgnoreMember] public ItemMoveInventoryType InventoryType => (ItemMoveInventoryType)Enum.ToObject(typeof(ItemMoveInventoryType), InventoryId);

            [Key(3)] public int Slot { get; set; }

            /// <summary>
            /// ブロックまたは列車インベントリの識別子
            /// Identifier for block or train inventory
            /// </summary>
            [Key(4)] public InventoryIdentifierMessagePack InventoryIdentifier { get; set; }

            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public ItemMoveInventoryInfoMessagePack() { }
            public ItemMoveInventoryInfoMessagePack(ItemMoveInventoryInfo info, int slot)
            {
                // メッセージパックでenumは重いらしいのでintを使う
                // MessagePack enum is heavy, so use int
                InventoryId = (int)info.ItemMoveInventoryType;
                Slot = slot;
                InventoryIdentifier = info.SubInventoryIdentifier;
            }
        }
    }
}
