using System.Collections.Generic;
using System.Linq;
using Game.Blueprint;
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

        public BlueprintProtocol(ServiceProvider serviceProvider)
        {
            _blueprintDatastore = serviceProvider.GetService<IBlueprintDatastore>();
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
                    return _blueprintDatastore.Delete(request.Name)
                        ? SuccessResponse(null)
                        : FailResponse(BlueprintFailureReason.NotFound);
                default:
                    return FailResponse(BlueprintFailureReason.UnknownOperation);
            }

            #region Internal

            ProtocolMessagePackBase HandleCreate(BlueprintRequest req)
            {
                if (string.IsNullOrWhiteSpace(req.Name)) return FailResponse(BlueprintFailureReason.InvalidName);
                if (req.Min == null || req.Max == null) return FailResponse(BlueprintFailureReason.InvalidRequest);

                // 範囲抽出。対象ブロック0なら空BPを作らず失敗を返す
                // Extract from the bounding box; reject empty selections
                var created = BlueprintCreateService.TryCreateFromArea(req.Name, req.Min.Vector3Int, req.Max.Vector3Int, out var blueprint);
                if (!created) return FailResponse(BlueprintFailureReason.EmptyArea);

                var registeredName = _blueprintDatastore.Register(blueprint);
                return SuccessResponse(registeredName);
            }

            BlueprintResponse SuccessResponse(string registeredName)
            {
                var blueprints = _blueprintDatastore.Blueprints.Select(b => new BlueprintMessagePack(b)).ToList();
                return new BlueprintResponse(true, BlueprintFailureReason.None, registeredName, blueprints);
            }

            BlueprintResponse FailResponse(BlueprintFailureReason reason)
            {
                return new BlueprintResponse(false, reason, null, new List<BlueprintMessagePack>());
            }

            #endregion
        }
    }
}
