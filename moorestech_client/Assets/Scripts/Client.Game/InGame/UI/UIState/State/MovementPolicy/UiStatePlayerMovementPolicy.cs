using System;
using Client.Game.InGame.Player;
using VContainer.Unity;

namespace Client.Game.InGame.UI.UIState.State.MovementPolicy
{
    /// <summary>
    ///     UIステートごとに自機の移動(WASD・ジャンプ・ダッシュ)を許すかの単一所有者。
    ///     メニュー画面を開いている間は移動を止め、ワールド操作の画面へ戻ったら解除する。
    ///     Single owner of whether the player may move (WASD, jump, sprint) in each UI state.
    ///     Movement stops while a menu screen is open and resumes on returning to a world-facing screen.
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
