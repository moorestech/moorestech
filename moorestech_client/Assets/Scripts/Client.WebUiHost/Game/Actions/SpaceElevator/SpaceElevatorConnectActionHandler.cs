using System.Threading;
using Client.Network.API;
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.UIState.State;
using Client.Game.InGame.UI.UIState.State.SubInventory;
using Cysharp.Threading.Tasks;
using Game.Block.Interface.Component;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Client.WebUiHost.Game.Actions
{
    /// <summary>
    /// 開いている宇宙エレベーター拠点を接続する
    /// space_elevator.connect: connects the open space elevator base
    /// </summary>
    public class SpaceElevatorConnectActionHandler : IActionHandler
    {
        public string ActionType => "space_elevator.connect";

        private readonly SubInventoryState _subInventoryState;

        public SpaceElevatorConnectActionHandler(SubInventoryState subInventoryState)
        {
            _subInventoryState = subInventoryState;
        }

        public async UniTask<ActionResult> ExecuteAsync(JObject payload)
        {
            if (_subInventoryState.CurrentSubInventorySource is not BlockSubInventorySource source) return Fail("block_not_open");
            if (!source.TryGetBlockIdentity(out _, out var blockTypeName, out var instanceId)) return Fail("block_not_open");
            if (blockTypeName != "SpaceElevatorBase") return Fail("invalid_block_type");

            // 結果は後続の状態詳細で画面へ戻る。ここは失敗理由だけを返す
            // The view updates from the following state detail; this only returns the failure reason
            var (response, reason) = await ClientContext.VanillaApi.Response.Block.ConnectSpaceElevator(source.BlockPosition, instanceId, CancellationToken.None);
            if (reason != PacketWaitCompletionReason.Received) return Fail(reason switch
            {
                PacketWaitCompletionReason.Timeout => "connect_timeout",
                PacketWaitCompletionReason.DeserializeFailed => "connect_invalid_response",
                _ => "connect_invalid_response",
            });
            if (response == null) return Fail("connect_invalid_response");
            return response.Result switch
            {
                SpaceElevatorConnectResult.Connected => ActionResult.Success(),
                SpaceElevatorConnectResult.AlreadyConnected => Fail("already_connected"),
                SpaceElevatorConnectResult.InsufficientCable => Fail("insufficient_cable"),
                SpaceElevatorConnectResult.NotSpaceElevatorBase => Fail("not_space_elevator_base"),
                SpaceElevatorConnectResult.InvalidRequest => Fail("connect_invalid_request"),
                SpaceElevatorConnectResult.BlockNotFound => Fail("connect_block_not_found"),
                SpaceElevatorConnectResult.InstanceMismatch => Fail("connect_instance_mismatch"),
                _ => Fail("connect_invalid_response"),
            };
            #region Internal
            ActionResult Fail(string reason)
            {
                Debug.LogWarning($"[SpaceElevatorConnect] 接続要求を拒否: {reason}");
                return ActionResult.Fail(reason);
            }
            #endregion
        }
    }
}
