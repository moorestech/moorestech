using System;
using Client.Game.InGame.BlockSystem.PlaceSystem.Targets;
using Game.UnlockState;

namespace Client.Game.InGame.UI.UIState.State.PlacementPick
{
    /// <summary>
    /// スポイトでピックした列車車両の選択可否を解決する
    /// Resolves whether an eyedropped train car is pickable
    /// </summary>
    public static class TrainCarPickResolver
    {
        public static bool TryResolvePickTarget(Guid trainCarGuid, IGameUnlockStateData unlockState, out TrainCarPlacementTarget resolvedTarget)
        {
            // 未解放車両はピック不可（スポイトで解放システムを迂回させない）
            // Locked train cars are not pickable; the eyedropper must not bypass the unlock system
            resolvedTarget = null;
            if (!unlockState.TrainCarUnlockStateInfos.TryGetValue(trainCarGuid, out var info) || !info.IsUnlocked) return false;

            resolvedTarget = new TrainCarPlacementTarget(trainCarGuid);
            return true;
        }
    }
}
