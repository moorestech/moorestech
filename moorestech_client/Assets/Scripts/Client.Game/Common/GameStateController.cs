using Client.Game.InGame.Player;
using Client.Game.InGame.UI.Challenge;
using Client.Game.InGame.UI.Inventory;
using Client.Game.InGame.UI.UIState;
using Client.Input;
using UnityEngine;
using VContainer;
using UniRx;
using System;

namespace Client.Game.Common
{
    public class GameStateController : MonoBehaviour
    {
        private static GameStateController _instance;
        
        [SerializeField] private CurrentChallengeHudView currentChallengeHudView;
        private HotBarView _hotBarView;
        private static readonly Subject<GameStateType> _onStateChanged = new();
        public static IObservable<GameStateType> OnStateChanged => _onStateChanged;
        public static GameStateType CurrentState { get; private set; } = GameStateType.InGame;
        
        private void Awake()
        {
            _instance = this;
        }
        
        [Inject]
        public void Construct(HotBarView hotBarView)
        {
            _hotBarView = hotBarView;
        }
        
        public void Start()
        {
            ChangeState(GameStateType.InGame);
        }
        
        public static void ChangeState(GameStateType gameStateType)
        {
            CurrentState = gameStateType;
            _onStateChanged.OnNext(gameStateType);
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
            PlayerSystemContainer.Instance.PlayerObjectController.SetActive(true);
            _hotBarView.SetActive(true);
            currentChallengeHudView.SetActive(true);
            
            InputManager.MouseCursorVisible(false);
        }
        
        private void SetSkitState()
        {
            PlayerSystemContainer.Instance.PlayerObjectController.SetActive(false);
            _hotBarView.SetActive(false);
            currentChallengeHudView.SetActive(false);
            
            InputManager.MouseCursorVisible(true);
        }
        
        private void SetCutSceneState()
        {
            PlayerSystemContainer.Instance.PlayerObjectController.SetActive(false);
            _hotBarView.SetActive(false);
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
