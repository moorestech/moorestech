using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace MainGame.Control.UI
{
    public class UIControl : MonoBehaviour
    {
        private  IUIState _currentState;
        private MoorestechInputSettings _inputSettings;
        
        [SerializeField] private GameObject pauseMenu;
        [SerializeField] private GameObject playerInventory;

        private void Start()
        {
            _inputSettings = new MoorestechInputSettings();
            _inputSettings.Enable();

            //ステートマシンの設定
            var gameScreen = new GameScreen();
            var inventory = new PlayerInventory(gameScreen);
            var pause = new PauseMenu(gameScreen,_inputSettings,pauseMenu);
            gameScreen.Construct(inventory,pause,_inputSettings);
            _currentState = gameScreen;
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