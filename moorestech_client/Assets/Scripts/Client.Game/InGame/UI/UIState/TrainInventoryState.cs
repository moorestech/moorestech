using Client.Common.Asset;
using Client.Game.InGame.Context;
using Client.Game.InGame.Control;
using Client.Game.InGame.Entity.Object;
using Client.Game.InGame.UI.Inventory.Block;
using Client.Game.InGame.UI.Inventory.Main;
using Client.Game.InGame.UI.Inventory.Train;
using Client.Input;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Client.Game.InGame.UI.UIState
{
    public class TrainInventoryState : IUIState
    {
        private const string AddressablePath = "InGame/UI/Inventory/TrainInventoryView";
        
        private readonly PlayerInventoryViewController _playerInventoryViewController;
        
        private ITrainInventoryView _trainInventoryView;
        
        
        public void OnEnter(UIStateEnum lastStateEnum)
        {
            if (!BlockClickDetect.TryGetCursorOnComponent<TrainEntityObject>(out var trainEntity)) return;
            
            LoadTrainInventory().Forget();
            
            #region Internal
            
            async UniTask LoadTrainInventory()
            {
                using var loadedInventory = await AddressableLoader.LoadAsync<GameObject>(AddressablePath);
                
                InputManager.MouseCursorVisible(true);
                
                _trainInventoryView = ClientContext.DIContainer.Instantiate(loadedInventory.Asset, _playerInventoryViewController.SubInventoryParent).GetComponent<ITrainInventoryView>();
                _trainInventoryView.Initialize(trainEntity);
                
                _playerInventoryViewController.SetActive(true);
                _playerInventoryViewController.SetSubInventory(_trainInventoryView);
            }
                
            #endregion
        }
        public UIStateEnum GetNextUpdate()
        {
            throw new System.NotImplementedException();
        }
        public void OnExit()
        {
            throw new System.NotImplementedException();
        }
    }
}