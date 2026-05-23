using System;
using Client.Game.InGame.Player.StateController;
using Client.Game.InGame.Train.Unit;
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
        // PauseMenu 等 UI 中断中も Riding を維持するため、UIStateEnum ではなくドメイン真実 (IsRiding) を見る。
        // Look at the domain truth (IsRiding) instead of UIStateEnum so PauseMenu etc. preserve Riding state.
        [Inject] private TrainCarRidingState _trainCarRidingState;

        public event Action<UIStateEnum> OnStateChanged;
        public UIStateEnum CurrentState { get; private set; } = UIStateEnum.GameScreen;

        private void Start()
        {
            _uiStateDictionary.GetState(CurrentState).OnEnter(new UITransitContext(CurrentState));
            // 初期状態を Player 側にも同期する。
            // Sync the initial state to the player side as well.
            _playerStateController.SetState(ResolvePlayerState());
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

            // プレイヤー側ステートを先に押し出してから UI 側購読者へ通知する。
            // Push the player-side state first, then notify UI-side subscribers.
            _playerStateController.SetState(ResolvePlayerState());

            OnStateChanged?.Invoke(CurrentState);
        }

        // PlayerState はドメイン真実 (TrainCarRidingState.IsRiding) で決まる。
        // UIStateEnum の値（PauseMenu / TrainHUDScreen / GameScreen 等）は遷移トリガーであって判定基準ではない。
        // PlayerState is decided by the domain truth (TrainCarRidingState.IsRiding).
        // UIStateEnum values are transition triggers, not the deciding factor.
        private PlayerStateEnum ResolvePlayerState()
        {
            return _trainCarRidingState.IsRiding ? PlayerStateEnum.Riding : PlayerStateEnum.Normal;
        }
    }
}
