using MainGame.Control.UI.UIState.UIObject;
using MainGame.UnityView.Control.MouseKeyboard;
using MainGame.UnityView.UI.CraftRecipe;

namespace MainGame.UnityView.UI.UIState
{
    public class BlockInventoryState : IUIState
    {
        private readonly MoorestechInputSettings _inputSettings;
        private readonly BlockInventoryObject _blockInventory;
        private readonly ItemListViewer _itemListViewer;
        private readonly ItemRecipePresenter _itemRecipePresenter;
        
        private readonly IBlockClickDetect _blockClickDetect;

        public BlockInventoryState(MoorestechInputSettings inputSettings, BlockInventoryObject blockInventory,
            IBlockClickDetect blockClickDetect,
            ItemListViewer itemListViewer,ItemRecipePresenter itemRecipePresenter)
        {
            _itemListViewer = itemListViewer;
            _itemRecipePresenter = itemRecipePresenter;
            _blockClickDetect = blockClickDetect;
            _inputSettings = inputSettings;
            _blockInventory = blockInventory;
            blockInventory.gameObject.SetActive(false);
        }

        public bool IsNext()
        {
            return _inputSettings.UI.CloseUI.triggered || _inputSettings.UI.OpenInventory.triggered || _itemRecipePresenter.IsClicked;
        }

        public UIStateEnum GetNext()
        {
            if (_inputSettings.UI.CloseUI.triggered || _inputSettings.UI.OpenInventory.triggered)
            {
                return UIStateEnum.GameScreen;
            }

            if (_itemRecipePresenter.IsClicked)
            {
                return UIStateEnum.RecipeViewer;
            }

            return UIStateEnum.BlockInventory;
        }

        public void OnEnter(UIStateEnum lastStateEnum)
        {
            var blockPos = _blockClickDetect.GetClickPosition();
            
            //その位置のブロックインベントリを取得するパケットを送信する
            //実際にインベントリのパケットを取得できてからUIを開くため、実際の開く処理はNetworkアセンブリで行う
            //ここで呼び出す処理が多くなった場合イベントを使うことを検討する
            //todo イベント化　_requestBlockInventoryProtocol.Send(blockPos.x,blockPos.y);
            //todo イベント化　_sendBlockInventoryOpenCloseControl.Send(blockPos.x,blockPos.y,true);
            //_itemMoveService.SetBlockPosition(blockPos.x,blockPos.y);
            
            _itemListViewer.gameObject.SetActive(true);
            _blockInventory.gameObject.SetActive(true);
        }

        public void OnExit()
        {
            _blockInventory.gameObject.SetActive(false);
            _itemListViewer.gameObject.SetActive(false);
        }
    }
}