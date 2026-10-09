using System.Collections.Generic;
using System.Linq;
using System.Text;
using Client.Game.InGame.BlockSystem.PlaceSystem.Util;
using Game.Block.Interface;
using Game.Blueprint;
using Server.Protocol.PacketResponse;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Blueprint.Paste
{
    /// <summary>
    ///     貼り付け列のうち置ける要素を設置プロトコルへ変換して送る
    ///     Converts the placeable elements of a paste run into the place protocol and sends them
    /// </summary>
    public static class BlueprintPastePlaceSender
    {
        // 手編集セーブのnull設定は空設定として送る
        // Send null settings from hand-edited saves as empty settings
        private static readonly Dictionary<string, string> EmptySettings = new();

        public static void SendPlaceable(List<BlueprintPlacementElement> placements, List<bool> placeableFlags)
        {
            // 置けない要素は落とし、置けるものが無ければ送信自体を見送る
            // Drop unplaceable elements and skip the send entirely when nothing remains
            var placeInfos = new List<PlaceInfo>();
            for (var i = 0; i < placements.Count; i++)
            {
                if (placeableFlags[i]) placeInfos.Add(ToPlaceInfo(placements[i]));
            }

            if (placeInfos.Count == 0)
            {
                Debug.Log("[BlueprintPaste] release skipped: no placeable blocks");
                return;
            }

            PlaceBlockProtocolSender.SendPlaceBlockProtocol(placeInfos);
        }

        private static PlaceInfo ToPlaceInfo(BlueprintPlacementElement placement)
        {
            var createParams = (placement.Settings ?? EmptySettings)
                .Select(kvp => new BlockCreateParam(kvp.Key, Encoding.UTF8.GetBytes(kvp.Value)))
                .ToArray();

            return new PlaceInfo
            {
                Position = placement.Position,
                Direction = placement.Direction,
                VerticalDirection = BlockVerticalDirection.Horizontal,
                BlockId = placement.BlockId,
                Placeable = true,
                CreateParams = createParams,
            };
        }
    }
}
