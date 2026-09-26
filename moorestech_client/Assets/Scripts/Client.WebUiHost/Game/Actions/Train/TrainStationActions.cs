using System.Threading;
using Core.Master;
using UnityEngine;
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.UIState.State;
using Client.Game.InGame.UI.UIState.State.SubInventory;
using Client.Network.API;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json.Linq;
using static Mooresmaster.Model.BlocksModule.BlockMasterElement;

namespace Client.WebUiHost.Game.Actions
{
    // 開いている駅ブロックの駅名を設定する
    // Set the name of the open station block
    public class TrainStationSetNameActionHandler : IActionHandler
    {
        public string ActionType => "train_station.set_name";
        private readonly SubInventoryState _subInventoryState;

        public TrainStationSetNameActionHandler(SubInventoryState subInventoryState)
        {
            _subInventoryState = subInventoryState;
        }

        public async UniTask<ActionResult> ExecuteAsync(JObject payload)
        {
            if (payload?["name"] is not JValue { Type: JTokenType.String } name) return Reject("invalid_payload");
            if (_subInventoryState.CurrentSubInventorySource is not BlockSubInventorySource source) return Reject("block_not_open");
            if (source.BlockTypeName != BlockTypeConst.TrainStation) return Reject("invalid_block_type");

            // 駅名の正本はサーバーのブロック状態
            // Keep server block state as the source of truth for station names
            var response = await ClientContext.VanillaApi.Response.Train.SetTrainStationName(source.BlockPosition, (string)name, CancellationToken.None);
            if (response == null || !response.Success) return Reject($"set_name_failed:{response?.FailureReason}");
            return ActionResult.Success();

            #region Internal

            ActionResult Reject(string reason)
            {
                Debug.LogWarning($"[TrainStationAction] rejected: {reason}");
                return ActionResult.Fail(reason);
            }

            #endregion
        }
    }
}
