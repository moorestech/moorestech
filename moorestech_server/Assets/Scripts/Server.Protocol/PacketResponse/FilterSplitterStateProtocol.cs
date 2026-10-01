using System;
using System.Collections.Generic;
using Core.Master;
using Game.Block.Blocks.FilterSplitter;
using Game.Context;
using Server.Protocol.PacketResponse.Util.FilterSplitter;
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

        public ProtocolMessagePackBase GetResponse(byte[] payload, int requesterPlayerId)
        {
            return FilterSplitterStateHandler.Handle(payload);
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
