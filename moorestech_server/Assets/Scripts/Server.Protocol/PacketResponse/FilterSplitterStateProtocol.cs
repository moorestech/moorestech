using System;
using System.Collections.Generic;
using Core.Master;
using Game.Block.Blocks.FilterSplitter;
using Game.Context;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using Server.Util.MessagePack;
using UnityEngine;

namespace Server.Protocol.PacketResponse
{
    /// <summary>
    /// FilterSplitter ブロックのフィルター設定を取得・更新するプロトコル。
    /// Operation により Get / SetMode / SetFilterItem を切り替える。
    /// Protocol for getting/updating FilterSplitter filter configuration; dispatches by Operation.
    /// </summary>
    public class FilterSplitterStateProtocol : IPacketResponse
    {
        public const string ProtocolTag = "va:filterSplitterState";

        public FilterSplitterStateProtocol(ServiceProvider serviceProvider)
        {
        }

        public ProtocolMessagePackBase GetResponse(byte[] payload, PacketResponseContext context)
        {
            var request = MessagePackSerializer.Deserialize<FilterSplitterStateRequest>(payload);

            // malformed payload で Position が null の場合は早期に拒否する
            // Reject malformed payloads whose Position is null up-front
            if (request.Position == null) return FailResponse(FilterSplitterStateFailureReason.InvalidRequest);

            var block = ServerContext.WorldBlockDatastore.GetBlock(request.Position.Vector3Int);
            if (block == null) return FailResponse(FilterSplitterStateFailureReason.BlockNotFound);

            if (!block.ComponentManager.TryGetComponent<VanillaFilterSplitterComponent>(out var splitter))
            {
                return FailResponse(FilterSplitterStateFailureReason.NotFilterSplitter);
            }

            // 操作種別ごとに分岐し、最後に最新の全状態スナップショットを返す
            // Branch by operation type, then return latest full state snapshot
            switch (request.Operation)
            {
                case FilterSplitterOperation.Get:
                    break;
                case FilterSplitterOperation.SetMode:
                    if (!ValidateDirection(splitter, request.DirectionIndex)) return FailResponse(FilterSplitterStateFailureReason.InvalidDirection);
                    if (!Enum.IsDefined(typeof(FilterSplitterMode), request.Mode)) return FailResponse(FilterSplitterStateFailureReason.InvalidMode);
                    splitter.SetMode(request.DirectionIndex, request.Mode);
                    break;
                case FilterSplitterOperation.SetFilterItem:
                    if (!ValidateDirection(splitter, request.DirectionIndex)) return FailResponse(FilterSplitterStateFailureReason.InvalidDirection);
                    if (!ValidateSlot(splitter, request.SlotIndex)) return FailResponse(FilterSplitterStateFailureReason.InvalidSlot);
                    // EmptyItemId はクリア、それ以外は master 存在チェック
                    // EmptyItemId means clear; otherwise verify the item exists in the master
                    if (request.ItemId != ItemMaster.EmptyItemId && !MasterHolder.ItemMaster.ExistItemId(request.ItemId))
                        return FailResponse(FilterSplitterStateFailureReason.InvalidItem);
                    splitter.SetFilterItem(request.DirectionIndex, request.SlotIndex, request.ItemId);
                    break;
                default:
                    return FailResponse(FilterSplitterStateFailureReason.UnknownOperation);
            }

            return BuildSnapshotResponse(splitter);

            #region Internal

            static bool ValidateDirection(VanillaFilterSplitterComponent s, int directionIndex)
            {
                return 0 <= directionIndex && directionIndex < s.DirectionCount;
            }

            static bool ValidateSlot(VanillaFilterSplitterComponent s, int slotIndex)
            {
                return 0 <= slotIndex && slotIndex < s.FilterSlotCountPerDirection;
            }

            static FilterSplitterStateResponse FailResponse(FilterSplitterStateFailureReason reason)
            {
                return new FilterSplitterStateResponse(false, reason, 0, 0, new List<DirectionStatePack>());
            }

            static FilterSplitterStateResponse BuildSnapshotResponse(VanillaFilterSplitterComponent s)
            {
                var directions = new List<DirectionStatePack>(s.DirectionCount);
                for (var d = 0; d < s.DirectionCount; d++)
                {
                    var slots = s.GetFilterItems(d);
                    var filterItemIds = new List<ItemId>(slots.Count);
                    foreach (var id in slots) filterItemIds.Add(id);
                    directions.Add(new DirectionStatePack
                    {
                        Mode = s.GetMode(d),
                        FilterItemIds = filterItemIds,
                    });
                }
                return new FilterSplitterStateResponse(true, FilterSplitterStateFailureReason.None, s.DirectionCount, s.FilterSlotCountPerDirection, directions);
            }

            #endregion
        }

        #region MessagePack

        [MessagePackObject]
        public class FilterSplitterStateRequest : ProtocolMessagePackBase
        {
            [Key(2)] public Vector3IntMessagePack Position { get; set; }
            [Key(3)] public FilterSplitterOperation Operation { get; set; }
            [Key(4)] public int DirectionIndex { get; set; }
            [Key(5)] public int SlotIndex { get; set; }
            [Key(6)] public FilterSplitterMode Mode { get; set; }
            [Key(7)] public ItemId ItemId { get; set; }

            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public FilterSplitterStateRequest() { Tag = ProtocolTag; }

            // Operation ごとに必要なフィールドだけを設定する private コンストラクタ
            // Private constructor; static factories below set only the fields each Operation needs
            private FilterSplitterStateRequest(Vector3Int position, FilterSplitterOperation operation, int directionIndex, int slotIndex, FilterSplitterMode mode, ItemId itemId)
            {
                Tag = ProtocolTag;
                Position = new Vector3IntMessagePack(position);
                Operation = operation;
                DirectionIndex = directionIndex;
                SlotIndex = slotIndex;
                Mode = mode;
                ItemId = itemId;
            }

            public static FilterSplitterStateRequest CreateGetRequest(Vector3Int position)
            {
                return new FilterSplitterStateRequest(position, FilterSplitterOperation.Get, 0, 0, FilterSplitterMode.Default, ItemMaster.EmptyItemId);
            }

            public static FilterSplitterStateRequest CreateSetModeRequest(Vector3Int position, int directionIndex, FilterSplitterMode mode)
            {
                return new FilterSplitterStateRequest(position, FilterSplitterOperation.SetMode, directionIndex, 0, mode, ItemMaster.EmptyItemId);
            }

            public static FilterSplitterStateRequest CreateSetFilterItemRequest(Vector3Int position, int directionIndex, int slotIndex, ItemId itemId)
            {
                return new FilterSplitterStateRequest(position, FilterSplitterOperation.SetFilterItem, directionIndex, slotIndex, FilterSplitterMode.Default, itemId);
            }
        }

        [MessagePackObject]
        public class FilterSplitterStateResponse : ProtocolMessagePackBase
        {
            [Key(2)] public bool Success { get; set; }
            [Key(3)] public FilterSplitterStateFailureReason FailureReason { get; set; }
            [Key(4)] public int DirectionCount { get; set; }
            [Key(5)] public int FilterSlotCountPerDirection { get; set; }
            [Key(6)] public List<DirectionStatePack> Directions { get; set; }

            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public FilterSplitterStateResponse() { }

            public FilterSplitterStateResponse(bool success, FilterSplitterStateFailureReason failureReason, int directionCount, int filterSlotCountPerDirection, List<DirectionStatePack> directions)
            {
                Tag = ProtocolTag;
                Success = success;
                FailureReason = failureReason;
                DirectionCount = directionCount;
                FilterSlotCountPerDirection = filterSlotCountPerDirection;
                Directions = directions;
            }
        }

        [MessagePackObject]
        public class DirectionStatePack
        {
            [Key(0)] public FilterSplitterMode Mode { get; set; }
            [Key(1)] public List<ItemId> FilterItemIds { get; set; }
        }

        public enum FilterSplitterOperation
        {
            Get = 0,
            SetMode = 1,
            SetFilterItem = 2,
        }

        public enum FilterSplitterStateFailureReason
        {
            None = 0,
            BlockNotFound = 1,
            NotFilterSplitter = 2,
            InvalidDirection = 3,
            InvalidSlot = 4,
            UnknownOperation = 5,
            InvalidMode = 6,
            InvalidItem = 7,
            InvalidRequest = 8,
        }

        #endregion
    }
}
