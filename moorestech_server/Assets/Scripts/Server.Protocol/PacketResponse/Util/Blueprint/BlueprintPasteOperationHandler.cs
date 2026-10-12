using System;
using System.Collections.Generic;
using System.Linq;
using Common.Debug;
using Game.Blueprint;
using Game.Construction;
using Game.Entity.Interface;
using Game.PlacementTarget;
using Game.PlayerInventory.Interface;
using Game.UnlockState;
using Microsoft.Extensions.DependencyInjection;
using Server.Event.Notification;
using Server.Protocol.PacketResponse.Util.Blueprint.Planning;
using Server.Protocol.PacketResponse.Util.Construction;
using UnityEngine;

namespace Server.Protocol.PacketResponse.Util.Blueprint
{
    internal class BlueprintPasteOperationHandler
    {
        private readonly IBlueprintDatastore _blueprints;
        private readonly IPlayerInventoryDataStore _inventories;
        private readonly IGameUnlockStateDataController _unlockState;
        private readonly PlacementTargetCatalog _catalog;
        private readonly ConstructionWalletService _wallet;
        private readonly BlockCellPlacementExecutor _cells;
        private readonly NotificationService _notifications;
        private readonly IEntitiesDatastore _entities;

        internal BlueprintPasteOperationHandler(ServiceProvider serviceProvider)
        {
            _blueprints = serviceProvider.GetRequiredService<IBlueprintDatastore>();
            _inventories = serviceProvider.GetRequiredService<IPlayerInventoryDataStore>();
            _unlockState = serviceProvider.GetRequiredService<IGameUnlockStateDataController>();
            _catalog = serviceProvider.GetRequiredService<PlacementTargetCatalog>();
            _wallet = serviceProvider.GetRequiredService<ConstructionWalletService>();
            _cells = new BlockCellPlacementExecutor(_wallet);
            _notifications = serviceProvider.GetRequiredService<NotificationService>();
            _entities = serviceProvider.GetRequiredService<IEntitiesDatastore>();
        }

        internal BlueprintResponse Handle(BlueprintRequest request, int requesterPlayerId)
        {
            // 配置前に回転・原点・識別子を検証
            // Validate rotation, origins and identity before placement.
            if (!IsValid())
            {
                Debug.LogWarning($"[BlueprintPaste] invalid request rotation={request.RotationStep} origins={request.Origins?.Count} player={requesterPlayerId}");
                Notify(BlueprintFailureReason.InvalidRequest, 0);
                return Failure(BlueprintFailureReason.InvalidRequest);
            }
            var blueprint = _blueprints.Blueprints.FirstOrDefault(b => b.BlueprintGuid == Guid.Parse(request.BlueprintGuidStr));
            if (blueprint == null)
            {
                Debug.LogWarning($"[BlueprintPaste] blueprint not found guid={request.BlueprintGuidStr} player={requesterPlayerId}");
                Notify(BlueprintFailureReason.NotFound, 0);
                return Failure(BlueprintFailureReason.NotFound);
            }

            // サーバーには地形がないため、送信原点は地形解決済みとする
            // The server has no terrain; incoming origins have already resolved it
            var waived = DebugParameters.GetValueOrDefaultBool(DebugParameterKeys.FreeBlockPlacement);
            var inventory = _inventories.GetInventoryData(requesterPlayerId).MainOpenableInventory;
            var world = new ServerBlueprintPasteWorld(_catalog, _unlockState, waived);
            var origins = request.Origins.ConvertAll(origin => new BlueprintPasteOrigin(origin.Vector3Int, true));
            var plan = BlueprintPastePlanner.Plan(blueprint, origins, request.RotationStep, world,
                _wallet.GetQuery(requesterPlayerId), ConstructionMaterialAccounting.TallyHeld(inventory.InventoryItems));

            if (plan.CountCopies(BlueprintPasteCopyState.InvalidCoordinates) != 0)
            {
                Debug.LogWarning($"[BlueprintPaste] invalid coordinates blueprint={blueprint.BlueprintGuid} player={requesterPlayerId}");
                Notify(BlueprintFailureReason.InvalidRequest, 0);
                return Failure(BlueprintFailureReason.InvalidRequest);
            }

            // 全コピーをサーバーのプレイヤー位置から検証する
            // Validate every copy against the server's player position
            var playerEntityId = new EntityInstanceId(requesterPlayerId);
            if (!_entities.Exists(playerEntityId) || origins.Any(origin =>
                    !PlacementDistanceRule.IsWithinReachWithSyncTolerance(_entities.GetPosition(playerEntityId), origin.Position)))
            {
                Debug.LogWarning($"[BlueprintPaste] invalid placement distance or missing player player={requesterPlayerId}");
                Notify(BlueprintFailureReason.InvalidRequest, 0);
                return Failure(BlueprintFailureReason.InvalidRequest);
            }

            // 拒否・実行失敗を操作単位で通知
            // Report rejection and execution failures per operation.
            LogSkipped();
            var result = BlueprintPasteExecutor.Execute(plan, requesterPlayerId, _cells, inventory);
            NotifyIfAny(BlueprintFailureReason.PasteCostShortage, plan.CountCopies(BlueprintPasteCopyState.MaterialShortage) + result.CostShortageCopyCount);
            NotifyIfAny(BlueprintFailureReason.PasteNotUnlocked, plan.CountCopies(BlueprintPasteCopyState.NotUnlocked));
            NotifyIfAny(BlueprintFailureReason.PasteLineFailed, result.FailedLineCount);
            NotifyIfAny(BlueprintFailureReason.PastePlacementFailed, result.PlacementFailedCopyCount + plan.CountCopies(BlueprintPasteCopyState.NoResolvedBlocks));
            return new BlueprintResponse(true, BlueprintFailureReason.None,
                plan.CountCopies(BlueprintPasteCopyState.MaterialShortage) > 0 || result.HasCostShortage,
                result.PlacedCells);

            #region Internal

            void LogSkipped()
            {
                // BP欠損と配置重複を操作内で集計
                // Aggregate missing blueprint data and overlaps per operation.
                var firstDraft = plan.Copies[0].Draft;
                LogIfAny("missing block master", blueprint.Blocks.Count - firstDraft.Elements.Count);
                LogIfAny("line endpoint missing", plan.Copies.Sum(copy => copy.Draft.MissingEndpointLineCount));
                LogIfAny("unknown connect tool", plan.Copies.Sum(copy => copy.Draft.UnknownConnectToolLineCount));
                LogIfAny("line endpoint overlaps", plan.Copies.Sum(copy => copy.Draft.OverlappingEndpointLineCount));
                LogIfAny("block overlaps", plan.Copies.Sum(copy => copy.Draft.NonOverlapFlags.Count(flag => !flag)));
                LogIfAny("NoResolvedBlocks", plan.CountCopies(BlueprintPasteCopyState.NoResolvedBlocks));
                LogIfAny("AllOverlapped", plan.CountCopies(BlueprintPasteCopyState.AllOverlapped));
            }

            void LogIfAny(string reason, int count)
            {
                if (count == 0) return;
                Debug.LogWarning($"[BlueprintPaste] skipped {reason} count={count} blueprint={blueprint.BlueprintGuid} player={requesterPlayerId}");
            }

            bool IsValid()
            {
                return 0 <= request.RotationStep && request.RotationStep < 4 &&
                       request.Origins != null && 0 < request.Origins.Count && request.Origins.Count <= BlueprintRequest.MaxPasteOrigins &&
                       request.Origins.All(origin => origin != null) && Guid.TryParse(request.BlueprintGuidStr, out _);
            }

            void NotifyIfAny(BlueprintFailureReason reason, int count)
            {
                if (count == 0) return;
                Debug.LogWarning($"[BlueprintPaste] rejected {reason} count={count} player={requesterPlayerId}");
                Notify(reason, count);
            }

            void Notify(BlueprintFailureReason reason, int count)
            {
                _notifications.Notify(requesterPlayerId,
                    NotificationMessagePack.CreateOperationDenied($"denied.blueprint.{reason}", new[] { count.ToString() }));
            }

            BlueprintResponse Failure(BlueprintFailureReason reason)
            {
                return new BlueprintResponse(false, reason, false, new List<BlueprintPlacedCellMessagePack>());
            }

            #endregion
        }
    }
}
