using System;
using System.Threading;
using Client.Game.Context;
using Client.Network.API;
using Cysharp.Threading.Tasks;
using Game.PlayerInventory.Interface;
using MainGame.Network.Settings;
using MainGame.UnityView.Control;
using MainGame.UnityView.UI.Inventory;
using MainGame.UnityView.UI.Inventory.Main;
using MainGame.UnityView.UI.Inventory.Sub;
using MainGame.UnityView.UI.UIState.UIObject;

namespace MainGame.UnityView.UI.UIState
{
    public class PlayerInventoryState : IUIState
    {
        private readonly CraftInventoryView _craftInventory;
        private readonly PlayerInventoryViewController _playerInventoryViewController;
        private readonly LocalPlayerInventoryController _localPlayerInventoryController;

        private CancellationTokenSource _cancellationTokenSource;
        
        public PlayerInventoryState(CraftInventoryView craftInventory,PlayerInventoryViewController playerInventoryViewController,LocalPlayerInventoryController localPlayerInventoryController)
        {
            _craftInventory = craftInventory;
            _playerInventoryViewController = playerInventoryViewController;
            _localPlayerInventoryController = localPlayerInventoryController;
            
            craftInventory.SetActive(false);
        }

        public UIStateEnum GetNext()
        {
            if (InputManager.UI.CloseUI.GetKeyDown || InputManager.UI.OpenInventory.GetKeyDown) return UIStateEnum.GameScreen;

            return UIStateEnum.Current;
        }

        public void OnEnter(UIStateEnum lastStateEnum)
        {
            _craftInventory.SetActive(true);
            _playerInventoryViewController.SetActive(true);
            _playerInventoryViewController.SetSubInventory(new EmptySubInventory());
            
            _cancellationTokenSource = new CancellationTokenSource();
            UpdatePlayerInventory(_cancellationTokenSource.Token).Forget();
            
            InputManager.MouseCursorVisible(true);
        }

        /// <summary>
        /// 基本的にプレイヤーのインベントリはイベントによって逐次更新データが送られてくるため、これをする必要がない
        /// ただ、更新データが何らかの原因で送られてこなかったり、適用できなかった時のために、バックアップとしてインベントリが開いた際は更新をかけるようにしている
        /// </summary>
        private async UniTask UpdatePlayerInventory(CancellationToken ct)
        {
            var invResponse = await MoorestechContext.VanillaApi.Response.GetMyPlayerInventory(ct);
            
            for (var i = 0; i < PlayerInventoryConst.MainInventorySize; i++)
            {
                var item = invResponse.MainInventory[i];
                _localPlayerInventoryController.SetMainItem(i, item);
            }

            _localPlayerInventoryController.SetGrabItem(invResponse.GrabItem);
        }

        public void OnExit()
        {
            _cancellationTokenSource.Cancel();
            _cancellationTokenSource = null;
            
            _craftInventory.SetActive(false);
            _playerInventoryViewController.SetActive(false);
        }
    }
}