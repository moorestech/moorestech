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
    /// FilterSplitter ブロックのフィルター設定をGet/Updateする統合プロトコル。
    /// OperationType により取得・モード変更・スロット設定を切り替える。
    /// Combined Get/Update protocol for FilterSplitter filter configuration.
    /// </summary>
    public class FilterSplitterStateProtocol : IPacketResponse
    {
        public const string ProtocolTag = "va:filterSplitterState";

        public FilterSplitterStateProtocol(ServiceProvider serviceProvider)
        {
        }

        public ProtocolMessagePackBase GetResponse(byte[] payload)
        {
            var request = MessagePackSerializer.Deserialize<FilterSplitterStateRequest>(payload);

            var block = ServerContext.WorldBlockDatastore.GetBlock(request.Position.Vector3Int);
            if (block == null) return FailResponse(FilterSplitterStateFailureReason.BlockNotFound);

            if (!block.ComponentManager.TryGetComponent<VanillaFilterSplitterComponent>(out var splitter))
            {
                return FailResponse(FilterSplitterStateFailureReason.NotFilterSplitter);
            }

            // 操作種別ごとに分岐し、最後に最新の全状態スナップショットを返す
            // Branch by operation type, then return latest full state snapshot
            switch ((FilterSplitterOperation)request.Operation)
            {
                case FilterSplitterOperation.Get:
                    break;
                case FilterSplitterOperation.SetMode:
                    if (!ValidateDirection(splitter, request.DirectionIndex)) return FailResponse(FilterSplitterStateFailureReason.InvalidDirection);
                    splitter.SetMode(request.DirectionIndex, (FilterSplitterMode)request.Mode);
                    break;
                case FilterSplitterOperation.SetFilterItem:
                    if (!ValidateDirection(splitter, request.DirectionIndex)) return FailResponse(FilterSplitterStateFailureReason.InvalidDirection);
                    if (!ValidateSlot(splitter, request.SlotIndex)) return FailResponse(FilterSplitterStateFailureReason.InvalidSlot);
                    var itemId = ResolveItemId(request.ItemGuidStr);
                    splitter.SetFilterItem(request.DirectionIndex, request.SlotIndex, itemId);
                    break;
                default:
                    return FailResponse(FilterSplitterStateFailureReason.UnknownOperation);
            }

            return BuildSnapshotResponse(splitter);

            #region Internal

            static bool ValidateDirection(VanillaFilterSplitterComponent s, int directionIndex)
            {
                return directionIndex >= 0 && directionIndex < s.DirectionCount;
            }

            static bool ValidateSlot(VanillaFilterSplitterComponent s, int slotIndex)
            {
                return slotIndex >= 0 && slotIndex < s.FilterSlotCountPerDirection;
            }

            static ItemId ResolveItemId(string itemGuidStr)
            {
                if (string.IsNullOrEmpty(itemGuidStr)) return ItemMaster.EmptyItemId;
                if (!Guid.TryParse(itemGuidStr, out var guid) || guid == Guid.Empty) return ItemMaster.EmptyItemId;
                var idOrNull = MasterHolder.ItemMaster.GetItemIdOrNull(guid);
                return idOrNull ?? ItemMaster.EmptyItemId;
            }

            static FilterSplitterStateResponse FailResponse(FilterSplitterStateFailureReason reason)
            {
                return new FilterSplitterStateResponse(false, reason, 0, 0, new List<DirectionStatePack>());
            }

            FilterSplitterStateResponse BuildSnapshotResponse(VanillaFilterSplitterComponent s)
            {
                var directions = new List<DirectionStatePack>(s.DirectionCount);
                for (var d = 0; d < s.DirectionCount; d++)
                {
                    var slots = s.GetFilterItems(d);
                    var guidStrs = new List<string>(slots.Count);
                    foreach (var id in slots)
                    {
                        if (id == ItemMaster.EmptyItemId)
                        {
                            guidStrs.Add(string.Empty);
                        }
                        else
                        {
                            guidStrs.Add(MasterHolder.ItemMaster.GetItemMaster(id).ItemGuid.ToString());
                        }
                    }
                    directions.Add(new DirectionStatePack
                    {
                        Mode = (int)s.GetMode(d),
                        FilterItemGuids = guidStrs,
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
            [Key(3)] public int Operation { get; set; }
            [Key(4)] public int DirectionIndex { get; set; }
            [Key(5)] public int SlotIndex { get; set; }
            [Key(6)] public int Mode { get; set; }
            [Key(7)] public string ItemGuidStr { get; set; }

            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public FilterSplitterStateRequest() { }

            public FilterSplitterStateRequest(Vector3Int position, FilterSplitterOperation operation, int directionIndex, int slotIndex, FilterSplitterMode mode, string itemGuidStr)
            {
                Tag = ProtocolTag;
                Position = new Vector3IntMessagePack(position);
                Operation = (int)operation;
                DirectionIndex = directionIndex;
                SlotIndex = slotIndex;
                Mode = (int)mode;
                ItemGuidStr = itemGuidStr ?? string.Empty;
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
            [Key(0)] public int Mode { get; set; }
            [Key(1)] public List<string> FilterItemGuids { get; set; }
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
        }

        #endregion
    }
}
