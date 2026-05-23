using System;
using Client.Game.InGame.Player.StateController;
using UnityEngine;
using VContainer;

namespace Client.Game.InGame.UI.UIState
{
    public class UIStateControl : MonoBehaviour
    {
        [Inject] private UIStateDictionary _uiStateDictionary;
        // UIState → PlayerStateController の一本道で押し出す。逆方向参照は行わない。
        // Push player-state changes one-way from UIState; never reference the player side back.
        [Inject] private PlayerStateController _playerStateController;

        public event Action<UIStateEnum> OnStateChanged;
        public UIStateEnum CurrentState { get; private set; } = UIStateEnum.GameScreen;

        private void Start()
        {
            _uiStateDictionary.GetState(CurrentState).OnEnter(new UITransitContext(CurrentState));
            // 初期状態を Player 側にも同期する（GameScreen → Normal）。
            // Sync the initial state to the player side as well (GameScreen → Normal).
            _playerStateController.SetState(MapUIStateToPlayerState(CurrentState));
        }

        //UIステート
        // UI state
        private void Update()
        {
            // 更新チェック
            // Check for updates
            var nextContext = _uiStateDictionary.GetState(CurrentState).GetNextUpdate();
            if (nextContext == null) return;

            var lastState = CurrentState;
            nextContext.SetLastState(lastState);
            CurrentState = nextContext.NextStateEnum;

            //現在のUIステートを終了し、次のステートを呼び出す
            // Exit current UI state and call next state
            _uiStateDictionary.GetState(lastState).OnExit();
            _uiStateDictionary.GetState(CurrentState).OnEnter(nextContext);

            OnStateChanged?.Invoke(CurrentState);

            // プレイヤー側ステートも合わせて押し出す（PlayerStateController は自発的に切り替わらない設計）。
            // Push the player-side state as well (PlayerStateController never self-transitions).
            _playerStateController.SetState(MapUIStateToPlayerState(CurrentState));
        }

        // UIState → PlayerState の射影。現状は TrainHUDScreen のみ Riding に対応し、それ以外は Normal。
        // Projection UIState → PlayerState. Currently only TrainHUDScreen maps to Riding; everything else is Normal.
        private static PlayerStateEnum MapUIStateToPlayerState(UIStateEnum uiState)
        {
            return uiState == UIStateEnum.TrainHUDScreen ? PlayerStateEnum.Riding : PlayerStateEnum.Normal;
        }
    }
}
