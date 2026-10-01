using System;
using System.Collections.Generic;
using Core.Master;
using Game.Block.Blocks.FilterSplitter;
using Game.Context;
using MessagePack;
using UnityEngine;
using static Server.Protocol.PacketResponse.FilterSplitterStateProtocol;

namespace Server.Protocol.PacketResponse.Util.FilterSplitter
{
    // 操作の検証と状態取得を一箇所で処理する
    // Validate an operation and collect the latest state in one place
    internal static class FilterSplitterStateHandler
    {
        internal static FilterSplitterStateResponse Handle(byte[] payload)
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
                Debug.LogWarning($"[FilterSplitterState] 要求を拒否しました: {reason}");
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
    }
}
