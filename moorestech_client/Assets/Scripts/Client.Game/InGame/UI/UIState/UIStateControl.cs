using System;
using Client.Game.InGame.Control.ViewMode;
using UnityEngine;
using VContainer;

namespace Client.Game.InGame.UI.UIState
{
    public class UIStateControl : MonoBehaviour
    {
        private UIStateDictionary _uiStateDictionary;
        private PlayerViewModeController _playerViewModeController;
        
        public event Action<UIStateEnum> OnStateChanged;
        public UIStateEnum CurrentState { get; private set; }
        private bool _isInitialized;

        [Inject]
        public void Construct(UIStateDictionary uiStateDictionary, PlayerViewModeController playerViewModeController)
        {
            _uiStateDictionary = uiStateDictionary;
            _playerViewModeController = playerViewModeController;
        }
        
        public void Initialize(UIStateEnum initialState, UITransitContext initialContext)
        {
            _isInitialized = true;
            CurrentState = initialState;
            _playerViewModeController.SetUIState(CurrentState);
            _uiStateDictionary.GetState(CurrentState).OnEnter(initialContext);
        }
        
        // UI state
        private void Update()
        {
            // 具体ステートの操作より先に共通視点入力を反映する
            // Apply shared view input before the concrete state performs its gameplay operation
            _playerViewModeController.SetTextInputFocused(TextInputFocusProvider.IsFocused());
            _playerViewModeController.ManualUpdate();

            // 更新チェック
            // Check for updates
            var nextContext = _uiStateDictionary.GetState(CurrentState).GetNextUpdate();
            if (nextContext == null) return;

            var lastState = CurrentState;
            nextContext.SetLastState(lastState);

            //現在のUIステートを終了し、次のステートを呼び出す
            // Exit current UI state and call next state
            _uiStateDictionary.GetState(lastState).OnExit();
            CurrentState = nextContext.NextStateEnum;
            _playerViewModeController.SetUIState(CurrentState);
            _uiStateDictionary.GetState(CurrentState).OnEnter(nextContext);

            OnStateChanged?.Invoke(CurrentState);
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus || !_isInitialized) return;

            _playerViewModeController.RestoreAfterApplicationFocus();
            if (_uiStateDictionary.GetState(CurrentState) is IApplicationFocusRestorer focusRestorer)
                focusRestorer.RestoreAfterApplicationFocus();
        }
    }
}
