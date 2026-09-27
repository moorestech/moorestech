using System;
using Client.Game.InGame.Player;
using VContainer.Unity;

namespace Client.Game.InGame.UI.UIState.State.MovementPolicy
{
    /// <summary>
    ///     UIステート別の移動可否の単一所有者。
    ///     メニュー中は停止、復帰で解除。
    ///     Single owner of per-state movement permission.
    ///     Blocks movement in menus, restores on return.
    /// </summary>
    public sealed class UiStatePlayerMovementPolicy : IInitializable
    {
        private readonly UIStateControl _uiStateControl;
        private readonly PlayerSystemContainer _playerSystemContainer;

        public UiStatePlayerMovementPolicy(UIStateControl uiStateControl, PlayerSystemContainer playerSystemContainer)
        {
            _uiStateControl = uiStateControl;
            _playerSystemContainer = playerSystemContainer;
        }

        public void Initialize()
        {
            ApplyMovementLock(_uiStateControl.CurrentState);
            _uiStateControl.OnStateChanged += ApplyMovementLock;
        }

        private void ApplyMovementLock(UIStateEnum state)
        {
            _playerSystemContainer.PlayerObjectController.SetMovementLockedByUi(IsMenuScreen(state));
        }

        // Web UIが背景ディムを出す画面族と同じ集合（uiScreenRouting.ts の backdrop）。列車・スキットの入れ子ポーズは対象外
        // Same family as the screens the Web UI dims (backdrop in uiScreenRouting.ts); nested pauses of train/skit are out of scope
        internal static bool IsMenuScreen(UIStateEnum state)
        {
            return state switch
            {
                UIStateEnum.PlayerInventory => true,
                UIStateEnum.SubInventory => true,
                UIStateEnum.PauseMenu => true,
                UIStateEnum.ChallengeList => true,
                UIStateEnum.ResearchTree => true,
                UIStateEnum.BuildMenu => true,
                UIStateEnum.GameScreen => false,
                UIStateEnum.DeleteBar => false,
                UIStateEnum.PlaceBlock => false,
                UIStateEnum.Story => false,
                UIStateEnum.Debug => false,
                UIStateEnum.TrainHUDScreen => false,
                _ => throw new ArgumentOutOfRangeException(nameof(state), state, "未分類のUIStateEnum。メニュー画面かどうかをここで決めること"),
            };
        }
    }
}
