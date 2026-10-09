using System.Collections.Generic;
using Core.Inventory;
using Server.Protocol.PacketResponse.Util.Blueprint.Planning;
using Server.Protocol.PacketResponse.Util.Construction;
using Server.Protocol.PacketResponse.Util.ElectricWire.Connection;
using Server.Protocol.PacketResponse.Util.GearChain;
using UnityEngine;

namespace Server.Protocol.PacketResponse.Util.Blueprint
{
    public static class BlueprintPasteExecutor
    {
        public static BlueprintPasteExecutionResult Execute(BlueprintPastePlan plan, int playerId, BlockCellPlacementExecutor cellExecutor, IOpenableInventory inventory)
        {
            var failedLines = 0;
            var shortageCopies = 0;
            var failedCopies = 0;
            foreach (var copy in plan.EnumerateCopiesToPlace())
            {
                var placed = new HashSet<Vector3Int>();
                var costShortage = false;
                var placementFailed = false;

                // 自動配線せず、実際に置けた端点だけを記録する
                // Skip auto-connect and record only endpoints actually placed
                foreach (var element in copy.EnumerateElementsToPlace())
                {
                    var cell = cellExecutor.PlanCell(element.BlockId, playerId, inventory, plan.IsPaymentWaived);
                    if (!cell.IsAffordable)
                    {
                        Debug.LogWarning($"[BlueprintPaste] execution cost shortage pos={element.Position} player={playerId}");
                        costShortage = true;
                        break;
                    }
                    if (cellExecutor.TryPlaceCell(cell, element.BlockId, element.Position, element.Direction,
                            BlueprintPlacementCreateParams.From(element.Settings), inventory, out _))
                    {
                        placed.Add(element.Position);
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
                    if (!placed.Contains(line.PositionA) || !placed.Contains(line.PositionB))
                    {
                        Debug.LogWarning($"[BlueprintPaste] line restore failed: endpoint not placed a={line.PositionA} b={line.PositionB} player={playerId}");
                        failedLines++;
                        continue;
                    }
                    if (!ConnectLine(line)) failedLines++;
                }
                if (costShortage) shortageCopies++;
                if (placementFailed || (costShortage && placed.Count > 0)) failedCopies++;
            }
            cellExecutor.FlushRemainingCountChanges();
            return new BlueprintPasteExecutionResult(failedLines, shortageCopies, failedCopies);

            #region Internal

            bool ConnectLine(BlueprintPasteLine line)
            {
                // 電線は無料設置を適用し、チェーンは常に支払う
                // Waive wire costs in free placement; chains are always paid
                var connected = line.Kind == BlueprintPasteLineKind.ElectricWire
                    ? ElectricWireSystemUtil.TryConnect(line.PositionA, line.PositionB, playerId, line.ConnectToolGuid, plan.IsPaymentWaived, out _)
                    : GearChainSystemUtil.TryConnect(line.PositionA, line.PositionB, playerId, line.ConnectToolGuid, out _);
                if (!connected)
                    Debug.LogWarning($"[BlueprintPaste] line restore failed kind={line.Kind} a={line.PositionA} b={line.PositionB} player={playerId}");
                return connected;
            }

            #endregion
        }
    }
}
