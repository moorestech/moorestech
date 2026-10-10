using System;
using System.Collections.Generic;
using System.Linq;
using Client.Game.InGame.BlockSystem.PlaceSystem.Undo;
using Client.Game.InGame.BlockSystem.PlaceSystem.Util;
using Client.Game.InGame.Context;
using Cysharp.Threading.Tasks;
using Server.Protocol.PacketResponse;
using Server.Protocol.PacketResponse.Util.Blueprint.Planning;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Blueprint.Paste
{
    /// <summary>
    ///     ドラッグ順に貼付し、確定セルをUndo登録
    ///     Send drags in order and group confirmed cells into one undo record
    /// </summary>
    internal static class BlueprintPastePlaceSender
    {
        private static readonly Queue<PendingPaste> _pending = new();
        private static readonly BlueprintPasteRequestRunner _runner = new(new VanillaBlueprintPasteRequestTransport());
        private static bool _isProcessing;

        internal static void Send(Guid blueprintGuid, int rotationStep, BlueprintPastePlan plan)
        {
            // 解放時の原点を複製し、後のプレビュー更新から切り離す
            // Snapshot release origins independently of later preview updates
            var origins = plan.EnumerateCopiesToPlace().Select(copy => copy.Draft.Origin).ToList();
            if (origins.Count == 0)
            {
                Debug.Log("[BlueprintPaste] release skipped: no placeable blueprint copies");
                return;
            }

            var history = ClientDIContext.BuildOperationHistory;
            var reservation = history.Reserve();
            _pending.Enqueue(new PendingPaste(blueprintGuid, rotationStep, origins, history, reservation));
            if (_isProcessing)
            {
                Debug.Log($"[BlueprintPaste] queued drag behind pending paste count={_pending.Count}");
                return;
            }
            ProcessQueue().Forget();
        }

        private static async UniTask ProcessQueue()
        {
            _isProcessing = true;
            while (_pending.Count > 0)
            {
                var operation = _pending.Dequeue();
                // 操作間では不足状態を共有せず、操作内だけ逐次送信する
                // Each operation sends its chunks sequentially with an independent shortage stop
                var placedCells = await _runner.Run(operation.BlueprintGuid, operation.RotationStep, operation.Origins);
                var record = PlaceOperationRecord.CreateFromPlacedCells(placedCells);
                if (record.HasCells)
                {
                    operation.History.Complete(operation.Reservation, record);
                    PlaceBlockProtocolSender.ReportConfirmedPlacement(placedCells.Count);
                }
                else
                {
                    operation.History.Cancel(operation.Reservation);
                    Debug.Log("[BlueprintPaste] no confirmed cells; undo history skipped");
                }
            }
            _isProcessing = false;
        }

        private sealed class PendingPaste
        {
            public readonly Guid BlueprintGuid;
            public readonly int RotationStep;
            public readonly List<Vector3Int> Origins;
            public readonly BuildOperationHistory History;
            public readonly LinkedListNode<IBuildOperationRecord> Reservation;

            public PendingPaste(Guid blueprintGuid, int rotationStep, List<Vector3Int> origins,
                BuildOperationHistory history, LinkedListNode<IBuildOperationRecord> reservation)
            {
                BlueprintGuid = blueprintGuid;
                RotationStep = rotationStep;
                Origins = origins;
                History = history;
                Reservation = reservation;
            }
        }
    }
}
