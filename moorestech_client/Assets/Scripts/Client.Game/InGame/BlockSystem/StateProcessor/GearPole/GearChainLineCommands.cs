using Cysharp.Threading.Tasks;
using Mooresmaster.Localization.Generated;
using Client.Game.InGame.UI.UIState.State.DragDelete.Completion;
using System;
using Client.Game.InGame.BlockSystem.PlaceSystem.Undo.Removal;
using Client.Game.InGame.BlockSystem.StateProcessor.ConnectionLine;
using Client.Game.InGame.Context;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.StateProcessor.GearPole
{
    // 歯車チェーンの切断と復元を送る
    // Send gear-chain disconnect and restore requests
    public sealed class GearChainLineCommands : IConnectionLineCommands
    {
        public ConnectionLineKind Kind => ConnectionLineKind.GearChain;

        public async UniTask<DeleteCompletion> Disconnect(object key, Vector3Int posA, Vector3Int posB)
        {
            // サーバーが返した個別応答だけを成功とする
            // Only an individual server response confirms success
            var response = await ClientContext.VanillaApi.Response.Removal.DisconnectGearChain(posA, posB, Application.exitCancellationToken);
            if (response == null) return new DeleteCompletion(key, DeleteCompletionKind.Unconfirmed, LocalizationKeys.Ui.Delete.UnknownError);
            if (response.IsSuccess) return new DeleteCompletion(key, DeleteCompletionKind.Succeeded, null);
            var reason = response.Error == nameof(Server.Protocol.PacketResponse.Util.GearChain.GearChainDisconnectFailureReason.InventoryFull) ? LocalizationKeys.Ui.Delete.InventoryFull : LocalizationKeys.Ui.Delete.UnknownError;
            Debug.LogWarning($"[ConnectionDelete] rejected {key}: {response}");
            return new DeleteCompletion(key, DeleteCompletionKind.Rejected, reason);
        }

        public void SendRestore(IRemovalRestoreSender sender, Vector3Int posA, Vector3Int posB, Guid connectToolGuid)
        {
            sender.ConnectGearChain(posA, posB, connectToolGuid);
        }
    }
}
