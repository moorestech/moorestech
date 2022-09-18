using System;
using UnityEngine;
using VContainer;

namespace MainGame.UnityView.UI.UIState
{
    public class UIStateControl : MonoBehaviour
    {
        private  UIStateEnum _currentState = UIStateEnum.GameScreen;

        public UIStateEnum CurrentState => _currentState;

        private UIStateDictionary _uiStateDictionary;
        
        public event Action<UIStateEnum> OnStateChanged;
        
        [Inject]
        public void Construct(UIStateDictionary uiStateDictionary)
        {
            _uiStateDictionary = uiStateDictionary;
        }
        
        
        //UIステート
        private void Update()
        {
            //UIステートが変更されたら
            var state = _uiStateDictionary.GetState(_currentState).GetNext();
            if (state == UIStateEnum.Current) return;

            var lastState = _currentState;
            _currentState = state;
            
            //現在のUIステートを終了し、次のステートを呼び出す
            _uiStateDictionary.GetState(lastState).OnExit();
            _uiStateDictionary.GetState(_currentState).OnEnter(lastState);
            
            OnStateChanged?.Invoke(_currentState);
        }
    }
}