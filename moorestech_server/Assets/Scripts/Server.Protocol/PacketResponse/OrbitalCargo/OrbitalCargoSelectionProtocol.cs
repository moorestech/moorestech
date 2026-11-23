using System;
using Core.Master;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Context;
using Game.PlayerInventory.Interface;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using Server.Util.MessagePack;
using UnityEngine;

namespace Server.Protocol.PacketResponse
{
    /// <summary>
    ///     ロケット発射台・宇宙エレベーター拠点の積荷を選ぶ。選択の結果はブロック状態の更新を正とする（ADR 0106 5）
    ///     Selects the cargo of a launch pad or space elevator base; the block state update is the source of truth (ADR 0106 5)
    /// </summary>
    public class OrbitalCargoSelectionProtocol : IPacketResponse
    {
        public const string ProtocolTag = "va:orbitalCargoSelection";
        private readonly IPlayerInventoryDataStore _playerInventoryDataStore;

        internal OrbitalCargoSelectionProtocol(ServiceProvider serviceProvider)
        {
            _playerInventoryDataStore = serviceProvider.GetService<IPlayerInventoryDataStore>();
        }

        public ProtocolMessagePackBase GetResponse(byte[] payload, int requesterPlayerId)
        {
            var request = MessagePackSerializer.Deserialize<OrbitalCargoSelectionRequest>(payload);
            if (request.Position == null || !request.ExpectedInstanceId.HasValue || !request.PayloadItemId.HasValue) return Fail(OrbitalCargoSelectionFailureReason.InvalidRequest);

            // 座標と実体IDで対象を確かめ、選択部品の無いブロックは拒否する
            // Verify the target by position and instance ID, rejecting blocks without a cargo selector
            var block = ServerContext.WorldBlockDatastore.GetBlock(request.Position.Vector3Int);
            if (block == null) return Fail(OrbitalCargoSelectionFailureReason.BlockNotFound);
            if (block.BlockInstanceId.AsPrimitive() != request.ExpectedInstanceId.Value) return Fail(OrbitalCargoSelectionFailureReason.InstanceMismatch);
            if (!block.ComponentManager.TryGetComponent<IOrbitalCargoSelectorComponent>(out var selector)) return Fail(OrbitalCargoSelectionFailureReason.NotCargoSelector);

            // 前の種類の返却先は選んだプレイヤーの手持ち（ADR 0106 4）
            // Leftovers of the previous kind go to the selecting player's inventory (ADR 0106 4)
            var refundInventory = _playerInventoryDataStore.GetInventoryData(requesterPlayerId).MainOpenableInventory;
            var result = selector.SelectCargo(request.PayloadItemId.Value, refundInventory);
            // 部品側の拒否は部品が理由をログに出している
            // Component-side rejections are already logged by the component
            return result switch
            {
                OrbitalCargoSelectionResult.Success => new OrbitalCargoSelectionResponse(true, OrbitalCargoSelectionFailureReason.None),
                OrbitalCargoSelectionResult.NotCarriedHere => new OrbitalCargoSelectionResponse(false, OrbitalCargoSelectionFailureReason.NotCarriedHere),
                OrbitalCargoSelectionResult.Locked => new OrbitalCargoSelectionResponse(false, OrbitalCargoSelectionFailureReason.Locked),
                _ => throw new ArgumentOutOfRangeException(nameof(result), result, "通信の失敗理由へ写していない積荷選択の結果です"),
            };

            #region Internal

            OrbitalCargoSelectionResponse Fail(OrbitalCargoSelectionFailureReason reason)
            {
                Debug.LogWarning($"積荷の選択要求を拒否しました 理由:{reason} 座標:{request.Position?.Vector3Int}");
                return new OrbitalCargoSelectionResponse(false, reason);
            }

            #endregion
        }

        #region MessagePack

        [MessagePackObject]
        public class OrbitalCargoSelectionRequest : ProtocolMessagePackBase
        {
            [Key(2)] public Vector3IntMessagePack Position { get; }
            [Key(3)] public int? ExpectedInstanceId { get; }
            [Key(4)] public ItemId? PayloadItemId { get; }

            public OrbitalCargoSelectionRequest(Vector3Int position, BlockInstanceId expectedInstanceId, ItemId payloadItemId)
                : this(ProtocolTag, 0, new Vector3IntMessagePack(position), expectedInstanceId.AsPrimitive(), payloadItemId) { }

            [SerializationConstructor]
            public OrbitalCargoSelectionRequest(string tag, int sequenceId, Vector3IntMessagePack position, int? expectedInstanceId, ItemId? payloadItemId)
            {
                Tag = tag;
                SequenceId = sequenceId;
                Position = position;
                ExpectedInstanceId = expectedInstanceId;
                PayloadItemId = payloadItemId;
            }
        }

        [MessagePackObject]
        public class OrbitalCargoSelectionResponse : ProtocolMessagePackBase
        {
            [Key(2)] public bool Success { get; }
            [Key(3)] public OrbitalCargoSelectionFailureReason FailureReason { get; }

            public OrbitalCargoSelectionResponse(bool success, OrbitalCargoSelectionFailureReason failureReason)
                : this(ProtocolTag, 0, success, failureReason) { }

            [SerializationConstructor]
            public OrbitalCargoSelectionResponse(string tag, int sequenceId, bool success, OrbitalCargoSelectionFailureReason failureReason)
            {
                Tag = tag;
                SequenceId = sequenceId;
                Success = success;
                FailureReason = failureReason;
            }
        }

        public enum OrbitalCargoSelectionFailureReason
        {
            None = 0,
            InvalidRequest = 1,
            BlockNotFound = 2,
            InstanceMismatch = 3,
            NotCargoSelector = 4,
            NotCarriedHere = 5,
            Locked = 6,
        }

        #endregion
    }
}
