using System;
using System.Linq;
using Client.Game.InGame.BlockSystem.PlaceSystem.Util;
using Client.Game.InGame.Context;
using Game.Block.Interface;
using Server.Protocol.PacketResponse;
using Server.Protocol.PacketResponse.Util.Blueprint;
using Server.Protocol.PacketResponse.Util.Blueprint.Planning;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Blueprint.Paste
{
    /// <summary>
    ///     設置可能なBPコピーを送信し操作結果を記録する
    ///     Sends placeable blueprint copies and records the operation
    /// </summary>
    public static class BlueprintPastePlaceSender
    {
        public static void Send(Guid blueprintGuid, int rotationStep, BlueprintPastePlan plan)
        {
            // BP単位で許可された原点だけを送りサーバーで再判定する
            // Send only accepted whole-copy origins for server revalidation
            var copies = plan.EnumerateCopiesToPlace().ToList();
            if (copies.Count == 0)
            {
                Debug.Log("[BlueprintPaste] release skipped: no placeable blueprint copies");
                return;
            }
            ClientContext.VanillaApi.SendOnly.PasteBlueprint(blueprintGuid, rotationStep, copies.ConvertAll(copy => copy.Draft.Origin));

            // 従来と同じブロック単位のUndo履歴を記録する
            // Record undo history at the same block granularity as before
            var infos = copies.SelectMany(copy => copy.EnumerateElementsToPlace()).Select(element => new PlaceInfo
            {
                Position = element.Position,
                Direction = element.Direction,
                VerticalDirection = BlockVerticalDirection.Horizontal,
                BlockId = element.BlockId,
                Placeable = true,
                CreateParams = BlueprintPlacementCreateParams.From(element.Settings),
            }).ToList();
            PlaceBlockProtocolSender.RecordSentPlacement(infos);
        }
    }
}
