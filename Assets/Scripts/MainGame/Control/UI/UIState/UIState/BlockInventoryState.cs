using MainGame.Control.Game.MouseKeyboard;
using MainGame.Control.UI.Inventory.ItemMove;
using MainGame.Control.UI.UIState.UIObject;
using MainGame.Network.Send;
using MainGame.UnityView.UI.CraftRecipe;
using UnityEngine;

namespace MainGame.Control.UI.UIState.UIState
{
    public class BlockInventoryState : IUIState
    {
        private readonly MoorestechInputSettings _inputSettings;
        private readonly BlockInventoryObject _blockInventory;
        private readonly ItemListViewer _itemListViewer;
        
        private readonly RequestBlockInventoryProtocol _requestBlockInventoryProtocol;
        private readonly SendBlockInventoryOpenCloseControl _sendBlockInventoryOpenCloseControl;
        private readonly BlockInventoryMainInventoryItemMoveService _itemMoveService;
        
        private readonly IBlockClickDetect _blockClickDetect;

        public BlockInventoryState(MoorestechInputSettings inputSettings, BlockInventoryObject blockInventory,
            RequestBlockInventoryProtocol requestBlockInventoryProtocol,
            BlockInventoryMainInventoryItemMoveService itemMoveService,IBlockClickDetect blockClickDetect,SendBlockInventoryOpenCloseControl sendBlockInventoryOpenCloseControl,
            ItemListViewer itemListViewer)
        {
            _itemListViewer = itemListViewer;
            _requestBlockInventoryProtocol = requestBlockInventoryProtocol;
            _itemMoveService = itemMoveService;
            _blockClickDetect = blockClickDetect;
            _sendBlockInventoryOpenCloseControl = sendBlockInventoryOpenCloseControl;
            _inputSettings = inputSettings;
            _blockInventory = blockInventory;
            blockInventory.gameObject.SetActive(false);
        }

        public bool IsNext()
        {
            return _inputSettings.UI.CloseUI.triggered || _inputSettings.UI.OpenInventory.triggered;
        }

        public UIStateEnum GetNext()
        {
            if (_inputSettings.UI.CloseUI.triggered || _inputSettings.UI.OpenInventory.triggered)
            {
                return UIStateEnum.GameScreen;
            }

            return UIStateEnum.BlockInventory;
        }

        public void OnEnter(UIStateEnum lastStateEnum)
        {
            var blockPos = _blockClickDetect.GetClickPosition();
            
            //その位置のブロックインベントリを取得するパケットを送信する
            //実際にインベントリのパケットを取得できてからUIを開くため、実際の開く処理はNetworkアセンブリで行う
            //ここで呼び出す処理が多くなった場合イベントを使うことを検討する
            _requestBlockInventoryProtocol.Send(blockPos.x,blockPos.y);
            _sendBlockInventoryOpenCloseControl.Send(blockPos.x,blockPos.y,true);
            _itemMoveService.SetBlockPosition(blockPos.x,blockPos.y);
            
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