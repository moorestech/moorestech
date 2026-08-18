using System;
using System.Collections.Generic;
using System.Linq;
using Game.Blueprint;
using Game.UnlockState;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;

namespace Server.Protocol.PacketResponse
{
    /// <summary>
    /// ・BP作成/一覧取得/削除
    /// ・Operationで分岐
    /// Protocol for creating, listing, and deleting blueprints; dispatches by Operation.
    /// </summary>
    public class BlueprintProtocol : IPacketResponse
    {
        public const string ProtocolTag = "va:blueprint";

        private readonly IBlueprintDatastore _blueprintDatastore;
        private readonly IGameUnlockStateData _gameUnlockState;

        public BlueprintProtocol(ServiceProvider serviceProvider)
        {
            _blueprintDatastore = serviceProvider.GetService<IBlueprintDatastore>();
            _gameUnlockState = serviceProvider.GetService<IGameUnlockStateDataController>();
        }

        public ProtocolMessagePackBase GetResponse(byte[] payload, PacketResponseContext context)
        {
            var request = MessagePackSerializer.Deserialize<BlueprintRequest>(payload);

            switch (request.Operation)
            {
                case BlueprintOperation.Create:
                    return HandleCreate(request);
                case BlueprintOperation.GetAll:
                    return SuccessResponse(null);
                case BlueprintOperation.Delete:
                    return HandleDelete(request);
                default:
                    return FailResponse(BlueprintFailureReason.UnknownOperation);
            }

            #region Internal

            ProtocolMessagePackBase HandleCreate(BlueprintRequest req)
            {
                // 未解放中は状態を変える操作を拒否する（GetAllは読み取り専用のため対象外・ADR 0015）
                // Reject mutating operations while locked; the read-only GetAll stays open (ADR 0015)
                if (!_gameUnlockState.IsBlueprintUnlocked) return FailResponse(BlueprintFailureReason.NotUnlocked);
                if (string.IsNullOrWhiteSpace(req.Name)) return FailResponse(BlueprintFailureReason.InvalidName);
                if (req.Min == null || req.Max == null) return FailResponse(BlueprintFailureReason.InvalidRequest);

                // 範囲抽出。対象ブロック0なら空BPを作らず失敗を返す
                // Extract from the bounding box; reject empty selections
                var created = BlueprintCreateService.TryCreateFromArea(req.Name, req.Min.Vector3Int, req.Max.Vector3Int, out var blueprint);
                if (!created) return FailResponse(BlueprintFailureReason.EmptyArea);

                // 発行されたGuidを返す（名前は加工しないため返す意味が無い）
                // Returns the issued GUID; the name is untouched so there is nothing to report back
                var registeredGuid = _blueprintDatastore.Register(blueprint);
                return SuccessResponse(registeredGuid.ToString());
            }

            ProtocolMessagePackBase HandleDelete(BlueprintRequest req)
            {
                // 未解放中は状態を変える操作を拒否する（GetAllは読み取り専用のため対象外・ADR 0015）
                // Reject mutating operations while locked; the read-only GetAll stays open (ADR 0015)
                if (!_gameUnlockState.IsBlueprintUnlocked) return FailResponse(BlueprintFailureReason.NotUnlocked);
                if (!Guid.TryParse(req.BlueprintGuidStr, out var blueprintGuid)) return FailResponse(BlueprintFailureReason.InvalidRequest);

                return _blueprintDatastore.Delete(blueprintGuid)
                    ? SuccessResponse(null)
                    : FailResponse(BlueprintFailureReason.NotFound);
            }

            BlueprintResponse SuccessResponse(string registeredGuidStr)
            {
                var blueprints = _blueprintDatastore.Blueprints.Select(b => new BlueprintMessagePack(b)).ToList();
                return new BlueprintResponse(true, BlueprintFailureReason.None, registeredGuidStr, blueprints);
            }

            BlueprintResponse FailResponse(BlueprintFailureReason reason)
            {
                return new BlueprintResponse(false, reason, null, new List<BlueprintMessagePack>());
            }

            #endregion
        }
    }
}
