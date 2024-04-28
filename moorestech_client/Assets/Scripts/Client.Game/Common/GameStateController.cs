using System;
using Client.Game.InGame.Control;
using Client.Game.InGame.Player;
using Client.Game.InGame.UI.Inventory;
using Client.Input;
using Client.Skit;
using UnityEngine;

namespace Client.Game.Common
{
    public class GameStateController : MonoBehaviour
    {
        public static GameStateController Instance { get; private set; }

        [SerializeField] private SkitCamera skitCamera;
        [SerializeField] private InGameCameraController inGameCameraController;

        [SerializeField] private PlayerObjectController playerObjectController;

        [SerializeField] private HotBarView hotBarView;

        private void Awake()
        {
            Instance = this;
        }

        public void Start()
        {
            ChangeState(GameStateType.InGame);
        }

        public void ChangeState(GameStateType gameStateType)
        {
            switch (gameStateType)
            {
                case GameStateType.InGame:
                    SetInGameState();
                    break;
                case GameStateType.Skit:
                    SetSkitState();
                    break;
                case GameStateType.CutScene:
                    SetCutSceneState();
                    break;
            }
        }

        private void SetInGameState()
        {
            skitCamera.SetActive(false);
            inGameCameraController.SetActive(true);

            playerObjectController.SetActive(true);

            hotBarView.SetActive(true);

            InputManager.MouseCursorVisible(true);
        }

        private void SetSkitState()
        {
            skitCamera.SetActive(true);
            inGameCameraController.SetActive(false);

            playerObjectController.SetActive(false);

            hotBarView.SetActive(false);

            InputManager.MouseCursorVisible(false);
        }

        private void SetCutSceneState()
        {
        }
    }

    public enum GameStateType
    {
        InGame,
        Skit,
        CutScene,
    }
}