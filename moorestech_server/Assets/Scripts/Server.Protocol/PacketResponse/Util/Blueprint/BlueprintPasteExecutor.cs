using System.Collections.Generic;
using Core.Inventory;
using Server.Protocol.PacketResponse.Util.Blueprint.Planning;
using Server.Protocol.PacketResponse.Util.Construction;
using Server.Protocol.PacketResponse.Util.ElectricWire.Connection;
using Server.Protocol.PacketResponse.Util.ElectricWire.Placement;
using Server.Protocol.PacketResponse.Util.GearChain;
using UnityEngine;

namespace Server.Protocol.PacketResponse.Util.Blueprint
{
    internal static class BlueprintPasteExecutor
    {
        public static BlueprintPasteExecutionResult Execute(BlueprintPastePlan plan, int playerId, BlockCellPlacementExecutor cellExecutor, IOpenableInventory inventory)
        {
            var failedLines = 0;
            var stopForShortage = false;
            var shortageCopies = 0;
            var failedCopies = 0;
            var placedCells = new List<BlueprintPlacedCellMessagePack>();
            foreach (var copy in plan.EnumerateCopiesToPlace())
            {
                if (stopForShortage)
                {
                    shortageCopies++;
                    continue;
                }
                var placed = new HashSet<Vector3Int>();
                var costShortage = false;
                var copyFailedLines = 0;
                var placementFailed = false;

                // 自動配線せず配置成功端点を記録
                // Record placed endpoints without auto-wiring.
                foreach (var element in copy.EnumerateElementsToPlace())
                {
                    var cell = cellExecutor.PlanCell(element.BlockId, playerId, inventory, plan.IsPaymentWaived);
                    if (!cell.IsAffordable)
                    {
                        // BP単位の事前判定後に同期設置イベントで在庫が減ると、公開済みイベントを巻き戻せず既配置分が残る
                        // If synchronous placement events reduce inventory after whole-copy planning, published events cannot be rolled back and placed cells remain.
                        costShortage = true;
                        stopForShortage = true;
                        break;
                    }
                    if (cellExecutor.TryPlaceCell(cell, element.Position, element.Direction,
                            BlueprintPlacementCreateParams.From(element.Settings), inventory, out var block))
                    {
                        placed.Add(element.Position);
                        placedCells.Add(new BlueprintPlacedCellMessagePack(element.Position, (int)element.Direction,
                            (int)element.BlockId, block.BlockInstanceId.AsPrimitive()));
                    }
                    else
                    {
                        placementFailed = true;
                    }
                }

                // 既存ブロックを失敗端点の代わりに接続しない
                // Never substitute an existing block for a failed endpoint
                foreach (var line in copy.Draft.Lines)
                {
                    // 当該コピーの素材不足は一種類のログと通知へ集約する
                    // Aggregate this copy's material shortage into one log and notification kind.
                    if (costShortage) break;
                    if (!line.IsConnectable)
                    {
                        Debug.LogWarning($"[BlueprintPaste] line skipped reason={line.FailureReason} kind={line.Kind} tool={line.ConnectToolGuid} player={playerId}");
                        continue;
                    }
                    if (!placed.Contains(line.PositionA) || !placed.Contains(line.PositionB))
                    {
                        Debug.LogWarning($"[BlueprintPaste] line restore failed: endpoint not placed a={line.PositionA} b={line.PositionB} player={playerId}");
                        copyFailedLines++;
                        continue;
                    }
                    if (ConnectLine(line, out var lineShortage)) continue;
                    if (lineShortage)
                    {
                        costShortage = true;
                        stopForShortage = true;
                        break;
                    }
                    copyFailedLines++;
                }
                if (costShortage)
                {
                    if (placed.Count == 0) shortageCopies++;
                    else failedCopies++;
                }
                else
                {
                    failedLines += copyFailedLines;
                    if (placementFailed) failedCopies++;
                }
            }
            cellExecutor.FlushRemainingCountChanges();
            return new BlueprintPasteExecutionResult(failedLines, shortageCopies, failedCopies,
                stopForShortage, placedCells);

            #region Internal

            bool ConnectLine(BlueprintPasteLine line, out bool materialShortage)
            {
                // 無料時もチェーン代は支払う
                // Chains remain paid during free placement.
                bool connected;
                string reason;
                if (line.Kind == BlueprintPasteLineKind.ElectricWire)
                {
                    connected = ElectricWireSystemUtil.TryConnect(line.PositionA, line.PositionB,
                        playerId, line.ConnectToolGuid, plan.IsPaymentWaived, out var failure);
                    reason = failure.ToString();
                    materialShortage = failure == ElectricWirePlacementFailureReason.NoWireItem;
                }
                else
                {
                    connected = GearChainSystemUtil.TryConnect(line.PositionA, line.PositionB,
                        playerId, line.ConnectToolGuid, out var failure);
                    reason = failure.ToString();
                    materialShortage = failure == GearChainPlacementFailureReason.NoItem;
                }
                if (!connected && !materialShortage)
                    Debug.LogWarning($"[BlueprintPaste] line restore failed reason={reason} kind={line.Kind} tool={line.ConnectToolGuid} a={line.PositionA} b={line.PositionB} player={playerId}");
                return connected;
            }

            #endregion
        }
    }
}
