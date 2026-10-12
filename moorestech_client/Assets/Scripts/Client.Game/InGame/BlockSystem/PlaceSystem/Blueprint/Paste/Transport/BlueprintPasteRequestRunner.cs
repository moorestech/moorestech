using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Client.Game.InGame.Context;
using Cysharp.Threading.Tasks;
using Server.Protocol.PacketResponse;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Blueprint.Paste
{
    internal interface IBlueprintPasteRequestTransport
    {
        UniTask<BlueprintResponse> Send(BlueprintRequest request);
    }

    internal sealed class VanillaBlueprintPasteRequestTransport : IBlueprintPasteRequestTransport
    {
        public UniTask<BlueprintResponse> Send(BlueprintRequest request)
        {
            return ClientContext.VanillaApi.Response.Block.SendBlueprintRequest(request, CancellationToken.None);
        }
    }

    internal sealed class BlueprintPasteRequestRunner
    {
        private readonly IBlueprintPasteRequestTransport _transport;

        internal BlueprintPasteRequestRunner(IBlueprintPasteRequestTransport transport)
        {
            _transport = transport;
        }

        internal async UniTask<List<BlueprintPlacedCellMessagePack>> Run(Guid blueprintGuid, int rotationStep,
            List<Vector3Int> origins)
        {
            var placedCells = new List<BlueprintPlacedCellMessagePack>();
            // 確定応答を待ってから次の64原点を送る
            // Wait for confirmed results before sending the next 64 origins
            foreach (var request in BlueprintRequest.CreatePasteRequests(blueprintGuid, rotationStep, origins))
            {
                BlueprintResponse response;
                try
                {
                    response = await _transport.Send(request);
                }
                catch (Exception error)
                {
                    // 通信境界の例外を記録し、先行チャンクの確定セルを保持する
                    // Log network-boundary failures and retain confirmed cells from earlier chunks
                    Debug.LogError($"[BlueprintPaste] stopped: request failed {error}");
                    break;
                }
                if (response == null)
                {
                    Debug.LogWarning("[BlueprintPaste] stopped: response timed out or could not be decoded");
                    break;
                }
                if (response.PlacedCells == null || response.PlacedCells.Any(cell => cell == null || cell.Position == null))
                {
                    Debug.LogError("[BlueprintPaste] stopped: response has invalid placed cells");
                    break;
                }
                placedCells.AddRange(response.PlacedCells);
                if (!response.Success)
                {
                    Debug.LogWarning($"[BlueprintPaste] stopped: server refused {response.FailureReason}");
                    break;
                }
                if (!response.HasCostShortage) continue;
                Debug.LogWarning("[BlueprintPaste] stopped: server reported material shortage");
                break;
            }
            return placedCells;
        }
    }
}
