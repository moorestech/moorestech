using Client.Game.InGame.Control;
using Client.Game.InGame.Player;
using Client.Game.InGame.UI.Challenge;
using Client.Game.InGame.UI.Inventory;
using Client.Input;
using Client.Skit.Skit;
using UnityEngine;

namespace Client.Game.Common
{
    public class GameStateController : MonoBehaviour
    {
        private static GameStateController _instance;
        [SerializeField] private SkitCamera skitCamera;
        [SerializeField] private InGameCameraController inGameCameraController;
        
        [SerializeField] private PlayerObjectController playerObjectController;
        
        [SerializeField] private HotBarView hotBarView;
        [SerializeField] private CurrentChallengeHudView currentChallengeHudView;
        
        private void Awake()
        {
            _instance = this;
        }
        
        public void Start()
        {
            ChangeState(GameStateType.InGame);
        }
        
        public static void ChangeState(GameStateType gameStateType)
        {
            switch (gameStateType)
            {
                case GameStateType.InGame:
                    _instance.SetInGameState();
                    break;
                case GameStateType.Skit:
                    _instance.SetSkitState();
                    break;
                case GameStateType.CutScene:
                    _instance.SetCutSceneState();
                    break;
            }
        }
        
        private void SetInGameState()
        {
            skitCamera.SetActive(false);
            inGameCameraController.SetActive(true);
            
            playerObjectController.SetActive(true);
            
            hotBarView.SetActive(true);
            currentChallengeHudView.SetActive(true);
            
            InputManager.MouseCursorVisible(false);
        }
        
        private void SetSkitState()
        {
            skitCamera.SetActive(true);
            inGameCameraController.SetActive(false);
            
            playerObjectController.SetActive(false);
            
            hotBarView.SetActive(false);
            currentChallengeHudView.SetActive(false);
            
            InputManager.MouseCursorVisible(true);
        }
        
        private void SetCutSceneState()
        {
            skitCamera.SetActive(false);
            inGameCameraController.SetActive(false);
            
            playerObjectController.SetActive(false);
            
            hotBarView.SetActive(false);
            currentChallengeHudView.SetActive(false);
            
            InputManager.MouseCursorVisible(false);
        }
    }
    
    public enum GameStateType
    {
        InGame,
        Skit,
        CutScene,
    }
}