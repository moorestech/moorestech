using System;
using Client.Game.InGame.BlockSystem.PlaceSystem.Targets;
using Game.UnlockState;
using UnityEngine;

namespace Client.Game.InGame.UI.UIState.State.PlacementPick
{
    /// <summary>
    ///     カーソル下の接続線から、その線を引いた接続ツールをスポイト対象に解決する
    ///     Resolves the connect tool that drew the hovered line as the eyedropper target
    /// </summary>
    public static class ConnectionLinePickResolver
    {
        public static bool TryResolve(Guid lineConnectToolGuid, IGameUnlockStateData unlockState, out IPlacementTarget target)
        {
            target = null;
            // 未知・未解放の種類は理由を残して拾わない
            // Log why unknown or locked tool kinds cannot be picked
            if (!unlockState.ConnectToolUnlockStateInfos.TryGetValue(lineConnectToolGuid, out var info))
            {
                Debug.LogWarning($"[PlacementPick] line tool not in unlock state: {lineConnectToolGuid}");
                return false;
            }
            if (!info.IsUnlocked)
            {
                Debug.LogWarning($"[PlacementPick] line tool locked: {lineConnectToolGuid}");
                return false;
            }

            target = new ConnectToolPlacementTarget(lineConnectToolGuid);
            return true;
        }
    }
}
