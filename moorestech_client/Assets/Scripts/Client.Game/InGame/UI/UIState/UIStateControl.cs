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
            var initialContext = new UITransitContext(UIStateEnum.Current);
            _uiStateDictionary.GetState(CurrentState).OnEnter(initialContext);
        }

        //UIステート
        // UI state
        private void Update()
        {
            //UIステートが変更されたら
            // When UI state changes
            var nextContext = _uiStateDictionary.GetState(CurrentState).GetNextUpdate();
            if (nextContext.LastStateEnum == UIStateEnum.Current) return;

            var lastState = CurrentState;
            CurrentState = nextContext.LastStateEnum;

            //現在のUIステートを終了し、次のステートを呼び出す
            // Exit current UI state and call next state
            _uiStateDictionary.GetState(lastState).OnExit();
            _uiStateDictionary.GetState(CurrentState).OnEnter(nextContext);

            OnStateChanged?.Invoke(CurrentState);
        }
    }
}