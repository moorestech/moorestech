using System;
using System.Collections.Generic;
using System.Linq;
using Game.Blueprint;
using Game.UnlockState;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using Server.Event.Notification;

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
        private readonly NotificationService _notificationService;

        public BlueprintProtocol(ServiceProvider serviceProvider)
        {
            _blueprintDatastore = serviceProvider.GetService<IBlueprintDatastore>();
            _gameUnlockState = serviceProvider.GetService<IGameUnlockStateData>();
            _notificationService = serviceProvider.GetService<NotificationService>();
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
                if (!_gameUnlockState.IsBlueprintUnlocked) return NotUnlockedResponse();
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
                if (!_gameUnlockState.IsBlueprintUnlocked) return NotUnlockedResponse();
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

            // 未解放拒否は通信失敗と区別できるよう通知でも届ける（railEdit/electricWireExtendと同形のdenied ID）
            // Surface the locked rejection as a notification too, so it is distinguishable from a request failure (same denied-id shape as railEdit/electricWireExtend)
            BlueprintResponse NotUnlockedResponse()
            {
                if (context.PlayerId.HasValue)
                    _notificationService.Notify(context.PlayerId.Value, NotificationMessagePack.CreateOperationDenied($"denied.blueprint.{BlueprintFailureReason.NotUnlocked}", Array.Empty<string>()));

                return FailResponse(BlueprintFailureReason.NotUnlocked);
            }

            BlueprintResponse FailResponse(BlueprintFailureReason reason)
            {
                return new BlueprintResponse(false, reason, null, new List<BlueprintMessagePack>());
            }

            #endregion
        }
    }
}
