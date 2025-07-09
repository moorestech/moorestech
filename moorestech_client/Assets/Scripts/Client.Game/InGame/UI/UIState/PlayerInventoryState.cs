using System.Threading;
using Client.Game.InGame.Context;
using Client.Game.InGame.CraftTree.TreeView;
using Client.Game.InGame.UI.Inventory;
using Client.Game.InGame.UI.Inventory.Main;
using Client.Game.InGame.UI.Inventory.RecipeViewer;
using Client.Game.InGame.UI.KeyControl;
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
        private readonly CraftTreeViewManager _craftTreeViewManager;
        
        private CancellationTokenSource _cancellationTokenSource;
        
        public PlayerInventoryState(RecipeViewerView recipeViewerView,PlayerInventoryViewController playerInventoryViewController, LocalPlayerInventoryController localPlayerInventoryController, InitialHandshakeResponse handshakeResponse, CraftTreeViewManager craftTreeViewManager)
        {
            _recipeViewerView = recipeViewerView;
            _playerInventoryViewController = playerInventoryViewController;
            _localPlayerInventoryController = localPlayerInventoryController;
            _craftTreeViewManager = craftTreeViewManager;
            
            _playerInventoryViewController.SetActive(false); //TODO この辺のオンオフをまとめたい
            _recipeViewerView.SetActive(false);
            
            //インベントリの初期設定
            _localPlayerInventoryController.SetMainInventory(handshakeResponse.Inventory.MainInventory);
            
            _localPlayerInventoryController.SetGrabItem(handshakeResponse.Inventory.GrabItem);
        }
        
        public UIStateEnum GetNextUpdate()
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
            KeyControlDescription.Instance.SetText("Tab/ECS: インベントリを閉じる");
        }
        
        public void OnExit()
        {
            _cancellationTokenSource.Cancel();
            _cancellationTokenSource = null;
            
            _recipeViewerView.SetActive(false);
            _playerInventoryViewController.SetActive(false);
            _craftTreeViewManager.Hide();
        }
        
        /// <summary>
        ///     基本的にプレイヤーのインベントリはイベントによって逐次更新データが送られてくるため、これをする必要がない
        ///     ただ、更新データが何らかの原因で送られてこなかったり、適用できなかった時のために、バックアップとしてインベントリが開いた際は更新をかけるようにしている
        /// </summary>
        private async UniTask UpdatePlayerInventory(CancellationToken ct)
        {
            var invResponse = await ClientContext.VanillaApi.Response.GetMyPlayerInventory(ct);
            
            _localPlayerInventoryController.SetMainInventory(invResponse.MainInventory);
            
            _localPlayerInventoryController.SetGrabItem(invResponse.GrabItem);
        }
    }
}