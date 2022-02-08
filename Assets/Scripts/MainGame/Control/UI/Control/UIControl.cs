using MainGame.Control.Game;
using MainGame.Control.UI.Control.UIState;
using UnityEngine;

namespace MainGame.Control.UI.Control
{
    public class UIControl : MonoBehaviour
    {
        private  IUIState _currentState;
        private MoorestechInputSettings _inputSettings;

        [SerializeField] private BlockClickDetect _blockClickDetect;
        
        [SerializeField] private GameObject pauseMenu;
        [SerializeField] private GameObject playerInventory;
        [SerializeField] private GameObject blockInventory;

        private void Start()
        {
            _inputSettings = new MoorestechInputSettings();
            _inputSettings.Enable();

            //ステートマシンの設定
            var gameScreen = new GameScreenState();
            var inventory = new PlayerInventoryState(gameScreen,_inputSettings,playerInventory);
            var blockInventoryState = new BlockInventoryState(gameScreen,_inputSettings,blockInventory);
            var pause = new PauseMenuState(gameScreen,_inputSettings,pauseMenu);
            
            gameScreen.Construct(inventory,pause,blockInventoryState,_inputSettings,_blockClickDetect);
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