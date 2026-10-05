using System;
using Client.Game.InGame.BlockSystem.PlaceSystem.Targets;
using Game.UnlockState;

namespace Client.Game.InGame.UI.UIState.State.PlacementPick
{
    /// <summary>
    ///     カーソル下の接続線から、その線を引いた接続ツールをスポイト対象に解決する
    ///     Resolves the connect tool that drew the hovered line as the eyedropper target
    /// </summary>
    public static class ConnectionLinePickResolver
    {
        public static ConnectionLinePickResult Resolve(Guid lineConnectToolGuid, IGameUnlockStateData unlockState)
        {
            // 未解放の種類はスポイト不成立にする
            // Locked tool kinds fail the eyedropper with distinct reasons
            if (!unlockState.ConnectToolUnlockStateInfos.TryGetValue(lineConnectToolGuid, out var info)) return ConnectionLinePickResult.Failed(ConnectionLinePickOutcome.UnknownTool);
            if (!info.IsUnlocked) return ConnectionLinePickResult.Failed(ConnectionLinePickOutcome.Locked);

            return ConnectionLinePickResult.Picked(new ConnectToolPlacementTarget(lineConnectToolGuid));
        }
    }
}
