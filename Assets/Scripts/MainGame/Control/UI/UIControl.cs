using System;
using UnityEngine;

namespace MainGame.Control.UI
{
    public class UIControl : MonoBehaviour
    {
        private  IUIState _currentState;

        private void Start()
        {
            _currentState = new GameScreen();
        }
        
        //UIステート
        private void Update()
        {
            //UIステートが変更されたら
            if (!_currentState.IsNext()) return;
            
            //現在のUIステートを終了し、次のステートを呼び出す
            _currentState.OnExit();
            _currentState = _currentState.GetNext();
            _currentState.OnEnter();
        }
    }
}