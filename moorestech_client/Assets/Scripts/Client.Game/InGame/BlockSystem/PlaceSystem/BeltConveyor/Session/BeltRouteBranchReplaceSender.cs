using System.Collections.Generic;
using System.Threading;
using Client.Game.InGame.BlockSystem.PlaceSystem.Util;
using Client.Game.InGame.Context;
using Cysharp.Threading.Tasks;
using Game.Block.Interface;
using Server.Protocol.PacketResponse;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Session
{
    /// <summary>
    ///     分岐の確定。既存ベルトの撤去要求が受理されてから、分岐器と経路の設置要求を送る
    ///     Commits a branch: sends the placement of the splitter and route only after the existing belt's removal request is accepted
    /// </summary>
    public static class BeltRouteBranchReplaceSender
    {
        public static void ReplaceAndPlace(BeltRouteBranchOrigin branch, List<PlaceInfo> placeInfos)
        {
            ReplaceAsync(branch.Cell, branch.Belt.BlockInstanceId, placeInfos).Forget();
        }

        private static async UniTask ReplaceAsync(Vector3Int beltCell, BlockInstanceId expectedInstanceId, List<PlaceInfo> placeInfos)
        {
            // 個体IDを照合して、間に別のブロックへ変わっていたら撤去しない
            // Matches the instance id so a belt replaced by something else in the meantime is never removed
            var response = await ClientContext.VanillaApi.Response.Block.BlockRemoveIfInstance(beltCell, expectedInstanceId, CancellationToken.None);

            // 応答はタイムアウト・デコード失敗でnullになる外部データ
            // The response is external data and becomes null on timeout or decode failure
            if (response == null)
            {
                Debug.LogWarning($"[BeltRouteBranch] remove got no response (timeout or decode failure) at {beltCell}; branch placement skipped");
                return;
            }
            if (!response.Success)
            {
                Debug.LogWarning($"[BeltRouteBranch] remove refused: {response.FailureReason} at {beltCell}; branch placement skipped");
                return;
            }

            PlaceBlockProtocolSender.SendPlaceBlockProtocol(placeInfos);
        }
    }
}
