using System.Threading;
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.Inventory;
using Client.Game.InGame.UI.Inventory.Main;
using Client.Game.InGame.UI.Inventory.RecipeViewer;
using Client.Input;
using Client.Network.API;
using Cysharp.Threading.Tasks;
using Game.PlayerInventory.Interface;

namespace Client.Game.InGame.UI.UIState
{
    public class PlayerInventoryState : IUIState
    {
        private readonly RecipeViewerView _recipeViewerView;
        private readonly LocalPlayerInventoryController _localPlayerInventoryController;
        private readonly PlayerInventoryViewController _playerInventoryViewController;
        
        private CancellationTokenSource _cancellationTokenSource;
        
        public PlayerInventoryState(RecipeViewerView recipeViewerView,PlayerInventoryViewController playerInventoryViewController, LocalPlayerInventoryController localPlayerInventoryController, InitialHandshakeResponse handshakeResponse)
        {
            _recipeViewerView = recipeViewerView;
            _playerInventoryViewController = playerInventoryViewController;
            _localPlayerInventoryController = localPlayerInventoryController;
            
            _playerInventoryViewController.SetActive(false); //TODO この辺のオンオフをまとめたい
            _recipeViewerView.SetActive(false);
            
            //インベントリの初期設定
            for (var i = 0; i < PlayerInventoryConst.MainInventorySize; i++)
            {
                var item = handshakeResponse.Inventory.MainInventory[i];
                _localPlayerInventoryController.SetMainItem(i, item);
            }
            
            _localPlayerInventoryController.SetGrabItem(handshakeResponse.Inventory.GrabItem);
        }
        
        public UIStateEnum GetNext()
        {
            if (InputManager.UI.CloseUI.GetKeyDown || InputManager.UI.OpenInventory.GetKeyDown) return UIStateEnum.GameScreen;
            
            return UIStateEnum.Current;
        }
        
        public void OnEnter(UIStateEnum lastStateEnum)
        {
            _recipeViewerView.SetActive(true);
            _playerInventoryViewController.SetActive(true);
            _playerInventoryViewController.SetSubInventory(new EmptySubInventory());
            
            _cancellationTokenSource = new CancellationTokenSource();
            UpdatePlayerInventory(_cancellationTokenSource.Token).Forget();
            
            InputManager.MouseCursorVisible(true);
        }
        
        public void OnExit()
        {
            _cancellationTokenSource.Cancel();
            _cancellationTokenSource = null;
            
            _recipeViewerView.SetActive(false);
            _playerInventoryViewController.SetActive(false);
        }
        
        /// <summary>
        ///     基本的にプレイヤーのインベントリはイベントによって逐次更新データが送られてくるため、これをする必要がない
        ///     ただ、更新データが何らかの原因で送られてこなかったり、適用できなかった時のために、バックアップとしてインベントリが開いた際は更新をかけるようにしている
        /// </summary>
        private async UniTask UpdatePlayerInventory(CancellationToken ct)
        {
            var invResponse = await ClientContext.VanillaApi.Response.GetMyPlayerInventory(ct);
            
            for (var i = 0; i < PlayerInventoryConst.MainInventorySize; i++)
            {
                var item = invResponse.MainInventory[i];
                _localPlayerInventoryController.SetMainItem(i, item);
            }
            
            _localPlayerInventoryController.SetGrabItem(invResponse.GrabItem);
        }
    }
}