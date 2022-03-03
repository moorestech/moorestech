using MainGame.Control.Game.MouseKeyboard;
using MainGame.Control.UI.Inventory.ItemMove;
using MainGame.Network.Send;
using UnityEngine;

namespace MainGame.Control.UI.UIState.UIState
{
    public class BlockInventoryState : IUIState
    {
        private readonly IUIState _gameScreen;
        private readonly MoorestechInputSettings _inputSettings;
        private readonly GameObject _blockInventory;
        private readonly RequestBlockInventoryProtocol _requestBlockInventoryProtocol;
        private readonly SendBlockInventoryOpenCloseControl _sendBlockInventoryOpenCloseControl;
        private readonly BlockInventoryMainInventoryItemMoveService _itemMoveService;
        private readonly IBlockClickDetect _blockClickDetect;

        public BlockInventoryState(IUIState gameScreen, MoorestechInputSettings inputSettings, GameObject blockInventory,
            RequestBlockInventoryProtocol requestBlockInventoryProtocol,
            BlockInventoryMainInventoryItemMoveService itemMoveService,IBlockClickDetect blockClickDetect,SendBlockInventoryOpenCloseControl sendBlockInventoryOpenCloseControl)
        {
            _requestBlockInventoryProtocol = requestBlockInventoryProtocol;
            _itemMoveService = itemMoveService;
            _blockClickDetect = blockClickDetect;
            _sendBlockInventoryOpenCloseControl = sendBlockInventoryOpenCloseControl;
            _gameScreen = gameScreen;
            _inputSettings = inputSettings;
            _blockInventory = blockInventory;
            blockInventory.SetActive(false);
        }

        public bool IsNext()
        {
            return _inputSettings.UI.CloseUI.triggered || _inputSettings.UI.OpenInventory.triggered;
        }

        public IUIState GetNext()
        {
            if (_inputSettings.UI.CloseUI.triggered || _inputSettings.UI.OpenInventory.triggered)
            {
                return _gameScreen;
            }

            return this;
        }

        public void OnEnter()
        {
            var blockPos = _blockClickDetect.GetClickPosition();
            
            //その位置のブロックインベントリを取得するパケットを送信する
            //実際にインベントリのパケットを取得できてからUIを開くため、実際の開く処理はNetworkアセンブリで行う
            //ここで呼び出す処理が多くなった場合イベントを使うことを検討する
            _requestBlockInventoryProtocol.Send(blockPos.x,blockPos.y);
            _sendBlockInventoryOpenCloseControl.Send(blockPos.x,blockPos.y,true);
            _itemMoveService.SetBlockPosition(blockPos.x,blockPos.y);
            
            _blockInventory.SetActive(true);
        }

        public void OnExit() { _blockInventory.SetActive(false); }
    }
}