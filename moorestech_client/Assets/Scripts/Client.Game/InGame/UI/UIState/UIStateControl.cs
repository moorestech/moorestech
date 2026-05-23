using System;
using UnityEngine;
using VContainer;

namespace Client.Game.InGame.UI.UIState
{
    public class UIStateControl : MonoBehaviour
    {
        [Inject] private UIStateDictionary _uiStateDictionary;
        
        public event Action<UIStateEnum> OnStateChanged;
        public UIStateEnum CurrentState { get; private set; } = UIStateEnum.GameScreen;
        
        private void Start()
        {
            _uiStateDictionary.GetState(CurrentState).OnEnter(new UITransitContext(CurrentState));
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
        }
    }
}