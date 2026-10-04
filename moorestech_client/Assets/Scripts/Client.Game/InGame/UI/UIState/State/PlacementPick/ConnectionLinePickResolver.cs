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
            // 解放状態に無い種類・未解放の種類はスポイト自体を不成立にし、理由を分ける（Guid.Emptyを下流へ流さない）
            // An unknown or locked tool fails the eyedropper with distinct reasons (never pass Guid.Empty downstream)
            if (!unlockState.ConnectToolUnlockStateInfos.TryGetValue(lineConnectToolGuid, out var info)) return ConnectionLinePickResult.Failed(ConnectionLinePickOutcome.UnknownTool);
            if (!info.IsUnlocked) return ConnectionLinePickResult.Failed(ConnectionLinePickOutcome.Locked);

            return ConnectionLinePickResult.Picked(new ConnectToolPlacementTarget(lineConnectToolGuid));
        }
    }
}
