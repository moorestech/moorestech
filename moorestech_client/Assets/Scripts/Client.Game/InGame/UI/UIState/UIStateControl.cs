using System;
using UnityEngine;
using VContainer;

namespace Client.Game.InGame.UI.UIState
{
    public class UIStateControl : MonoBehaviour
    {
        private UIStateDictionary _uiStateDictionary;
        
        public event Action<UIStateEnum> OnStateChanged;
        public UIStateEnum CurrentState { get; private set; }
        [Inject]
        public void Construct(UIStateDictionary uiStateDictionary)
        {
            _uiStateDictionary = uiStateDictionary;
        }
        
        public void Initialize(UIStateEnum initialState, UITransitContext initialContext)
        {
            CurrentState = initialState;
            _uiStateDictionary.GetState(CurrentState).OnEnter(initialContext);
        }
        
        // UI state
        private void Update()
        {
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
            _uiStateDictionary.GetState(CurrentState).OnEnter(nextContext);

            OnStateChanged?.Invoke(CurrentState);
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus) return;

            if (_uiStateDictionary.GetState(CurrentState) is IApplicationFocusRestorer focusRestorer)
                focusRestorer.RestoreAfterApplicationFocus();
        }
    }
}
